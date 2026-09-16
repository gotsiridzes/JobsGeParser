using JobsGeParser.Alerts;
using JobsGeParser.Alerts.Channels;
using JobsGeParser.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobsGeParser.IntegrationTests;

/// <summary>
/// Claiming, retry, dead lettering and cancellation. The claim is raw SQL with SKIP LOCKED, so
/// this needs a real database; the channel is faked so outcomes can be scripted.
/// </summary>
[Collection("Alerts")]
public class AlertSendTests(AlertsFixture fixture)
{
	private Task<int> DrainAsync() => fixture.Resolve<AlertOutboxDrainer>().DrainOnceAsync();

	private Task<int> PumpAsync() => fixture.Resolve<AlertSendPump>().PumpOnceAsync();

	/// <summary>
	/// Produces exactly one pending instant message.
	/// </summary>
	private async Task<Guid> SeedPendingMessageAsync(int jobId = 600)
	{
		Guid subscriberId;

		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			await AlertsTestData.AddJobAsync(
				db,
				AlertsTestData.NewJob(jobId, firstSeenAt: fixture.Clock.GetUtcNow()),
				"it");

			var (subscriber, _) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db,
				new AlertCriteria(TitleContains: "backend"),
				matchFromAt: fixture.Clock.GetUtcNow().AddDays(-1));

			subscriberId = subscriber.Id;

			AlertsTestData.AddJobEvent(db, jobId);
			await db.SaveChangesAsync();
		}

		await DrainAsync();
		return subscriberId;
	}

	[Fact]
	public async Task PendingMessage_IsSentAndMarkedSent()
	{
		await fixture.ResetAsync();
		await SeedPendingMessageAsync();

		Assert.Equal(1, await PumpAsync());

		await using var verify = fixture.CreateScope();
		var db = verify.ServiceProvider.GetRequiredService<JobsDbContext>();
		var message = await db.AlertMessages.SingleAsync();

		Assert.Equal(AlertMessageStatus.Sent, message.Status);
		Assert.Equal(1, message.Attempts);
		Assert.NotNull(message.SentAt);
		Assert.Single(fixture.Email.Sent);
	}

	[Fact]
	public async Task AlreadySentMessage_IsNotSentAgain()
	{
		await fixture.ResetAsync();
		await SeedPendingMessageAsync(601);

		await PumpAsync();
		await PumpAsync();

		Assert.Single(fixture.Email.Sent);
	}

	[Fact]
	public async Task TransientFailures_AreRetriedWithGrowingDelayThenSucceed()
	{
		await fixture.ResetAsync();
		await SeedPendingMessageAsync(602);

		fixture.Email.Script(
			AlertSendResult.Transient("first"),
			AlertSendResult.Transient("second"));

		await PumpAsync();

		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			var afterFirst = await db.AlertMessages.AsNoTracking().SingleAsync();

			Assert.Equal(AlertMessageStatus.Pending, afterFirst.Status);
			Assert.Equal(1, afterFirst.Attempts);
			Assert.True(afterFirst.AvailableAt > fixture.Clock.GetUtcNow());
		}

		// Move past the backoff window for each retry.
		fixture.Clock.Advance(TimeSpan.FromMinutes(5));
		await PumpAsync();

		fixture.Clock.Advance(TimeSpan.FromMinutes(5));
		await PumpAsync();

		await using var verify = fixture.CreateScope();
		var verifyDb = verify.ServiceProvider.GetRequiredService<JobsDbContext>();
		var message = await verifyDb.AlertMessages.AsNoTracking().SingleAsync();

		Assert.Equal(AlertMessageStatus.Sent, message.Status);
		Assert.Equal(3, message.Attempts);
	}

	[Fact]
	public async Task PersistentFailure_DeadLettersAfterTheAttemptBudget()
	{
		await fixture.ResetAsync();
		var subscriberId = await SeedPendingMessageAsync(603);

		fixture.Email.AlwaysFailTransiently = true;

		// MaxSendAttempts is 3 in the test configuration.
		for (var i = 0; i < 4; i++)
		{
			await PumpAsync();
			fixture.Clock.Advance(TimeSpan.FromMinutes(10));
		}

		await using var verify = fixture.CreateScope();
		var db = verify.ServiceProvider.GetRequiredService<JobsDbContext>();
		var message = await db.AlertMessages.AsNoTracking().SingleAsync();
		var subscriber = await db.AlertSubscribers.AsNoTracking().SingleAsync(s => s.Id == subscriberId);

		Assert.Equal(AlertMessageStatus.DeadLettered, message.Status);
		Assert.True(subscriber.ConsecutiveFailures > 0);
	}

	[Fact]
	public async Task PermanentBadAddress_SuppressesTheSubscriberAndPausesTheirAlerts()
	{
		await fixture.ResetAsync();
		var subscriberId = await SeedPendingMessageAsync(604);

		fixture.Email.Script(AlertSendResult.Permanent("invalid recipient", suppressSubscriber: true));

		await PumpAsync();

		await using var verify = fixture.CreateScope();
		var db = verify.ServiceProvider.GetRequiredService<JobsDbContext>();

		var subscriber = await db.AlertSubscribers.AsNoTracking().SingleAsync(s => s.Id == subscriberId);
		var subscriptions = await db.AlertSubscriptions.AsNoTracking()
			.Where(s => s.SubscriberId == subscriberId)
			.ToListAsync();

		Assert.Equal(AlertSubscriberStatus.Suppressed, subscriber.Status);
		Assert.NotNull(subscriber.SuppressionReason);
		Assert.All(subscriptions, s => Assert.Equal(AlertSubscriptionStatus.Paused, s.Status));
	}

	[Fact]
	public async Task UnsubscribingBeforeTheSend_CancelsTheQueuedMessage()
	{
		await fixture.ResetAsync();
		var subscriberId = await SeedPendingMessageAsync(605);

		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			var subscriber = await db.AlertSubscribers.SingleAsync(s => s.Id == subscriberId);
			subscriber.Status = AlertSubscriberStatus.Unsubscribed;
			await db.SaveChangesAsync();
		}

		await PumpAsync();

		await using var verify = fixture.CreateScope();
		var verifyDb = verify.ServiceProvider.GetRequiredService<JobsDbContext>();
		var message = await verifyDb.AlertMessages.AsNoTracking().SingleAsync();

		Assert.Equal(AlertMessageStatus.Cancelled, message.Status);
		Assert.Empty(fixture.Email.Sent);
	}

	[Fact]
	public async Task ConfirmationEmail_IsSentToAnUnconfirmedAddress()
	{
		await fixture.ResetAsync();

		// Regression: the pre-send check used to require Confirmed for every message, which
		// cancelled every confirmation, since those always go to someone not yet confirmed.
		await using var scope = fixture.CreateScope();
		var service = scope.ServiceProvider.GetRequiredService<AlertSubscriptionService>();
		var result = await service.SubscribeAsync(
			Data.AlertChannelKind.Email,
			"new.person@example.com",
			"Backend",
			AlertCadence.Instant,
			new TimeOnly(9, 0),
			"Asia/Tbilisi",
			new AlertCriteria(TitleContains: "backend"));

		Assert.Equal(AlertSubscribeOutcome.Accepted, result.Outcome);

		await PumpAsync();

		var sent = Assert.Single(fixture.Email.Sent);
		Assert.Equal("new.person@example.com", sent.Address);
		Assert.Contains("/a/c/", sent.BodyText, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ResubmittingAnUnconfirmedAlert_SendsANewConfirmation()
	{
		await fixture.ResetAsync();

		async Task SubscribeAsync()
		{
			await using var scope = fixture.CreateScope();
			var service = scope.ServiceProvider.GetRequiredService<AlertSubscriptionService>();
			await service.SubscribeAsync(
				Data.AlertChannelKind.Email,
				"retry@example.com",
				"Backend",
				AlertCadence.Instant,
				new TimeOnly(9, 0),
				"Asia/Tbilisi",
				new AlertCriteria(TitleContains: "backend"));
		}

		await SubscribeAsync();

		// Past the 60 second resend gap.
		fixture.Clock.Advance(TimeSpan.FromMinutes(2));
		await SubscribeAsync();

		await using var verify = fixture.CreateScope();
		var db = verify.ServiceProvider.GetRequiredService<JobsDbContext>();

		Assert.Equal(2, await db.AlertMessages.CountAsync(m => m.Kind == AlertMessageKind.Confirmation));
		Assert.Equal(1, await db.AlertSubscriptions.CountAsync());
	}

	[Fact]
	public async Task SentMessage_CarriesTheRenderedBodyAndAnUnsubscribeLink()
	{
		await fixture.ResetAsync();
		await SeedPendingMessageAsync(606);

		await PumpAsync();

		var sent = Assert.Single(fixture.Email.Sent);

		Assert.Contains("Senior Backend Developer", sent.BodyText, StringComparison.Ordinal);
		// The List-Unsubscribe target is the API, because mail clients POST to it directly.
		Assert.StartsWith("https://api.example.test/a/u/", sent.UnsubscribeUrl!, StringComparison.Ordinal);
		Assert.False(string.IsNullOrWhiteSpace(sent.Subject));
	}
}
