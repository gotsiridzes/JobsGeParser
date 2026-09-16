using JobsGeParser.Alerts;
using JobsGeParser.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobsGeParser.IntegrationTests;

/// <summary>
/// Digest scheduling is timezone and calendar sensitive, so these drive a controllable clock
/// rather than waiting for wall time.
/// </summary>
[Collection("Alerts")]
public class AlertDigestTests(AlertsFixture fixture)
{
	private Task<int> DrainAsync() => fixture.Resolve<AlertOutboxDrainer>().DrainOnceAsync();

	private Task<int> BuildDigestsAsync() => fixture.Resolve<AlertDigestBuilder>().BuildDueDigestsAsync();

	/// <summary>
	/// Seeds a digest subscription with pending matches already recorded.
	/// </summary>
	private async Task<Guid> SeedPendingDigestAsync(
		int jobCount,
		string timeZoneId = "Asia/Tbilisi",
		TimeOnly? sendAt = null,
		int firstJobId = 200)
	{
		Guid subscriptionId;

		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();

			var (_, subscription) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db,
				new AlertCriteria(TitleContains: "backend"),
				cadence: AlertCadence.DailyDigest,
				timeZoneId: timeZoneId,
				digestSendLocalTime: sendAt ?? new TimeOnly(9, 0),
				matchFromAt: fixture.Clock.GetUtcNow().AddDays(-7));

			subscriptionId = subscription.Id;

			for (var i = 0; i < jobCount; i++)
			{
				var id = firstJobId + i;
				await AlertsTestData.AddJobAsync(
					db,
					AlertsTestData.NewJob(id, firstSeenAt: fixture.Clock.GetUtcNow()),
					"it");
				AlertsTestData.AddJobEvent(db, id);
			}

			await db.SaveChangesAsync();
		}

		await DrainAsync();
		return subscriptionId;
	}

	[Fact]
	public async Task BeforeTheLocalSendTime_NothingIsQueued()
	{
		await fixture.ResetAsync();
		// 06:00 UTC is 10:00 in Tbilisi, so ask for 23:00 local to stay in the future.
		fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2026-09-16T06:00:00Z"));

		await SeedPendingDigestAsync(2, sendAt: new TimeOnly(23, 0));

		Assert.Equal(0, await BuildDigestsAsync());
	}

	[Fact]
	public async Task AfterTheLocalSendTime_OneMessageBatchesEveryPendingJob()
	{
		await fixture.ResetAsync();
		fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2026-09-16T06:00:00Z"));

		var subscriptionId = await SeedPendingDigestAsync(3, sendAt: new TimeOnly(9, 0));

		Assert.Equal(1, await BuildDigestsAsync());

		await using var verify = fixture.CreateScope();
		var db = verify.ServiceProvider.GetRequiredService<JobsDbContext>();

		var messages = await db.AlertMessages.Where(m => m.Kind == AlertMessageKind.DailyDigest).ToListAsync();
		Assert.Single(messages);
		Assert.Contains("3 new jobs", messages[0].Subject!, StringComparison.OrdinalIgnoreCase);

		var deliveries = await db.AlertDeliveries.Where(d => d.SubscriptionId == subscriptionId).ToListAsync();
		Assert.Equal(3, deliveries.Count);
		Assert.All(deliveries, d => Assert.Equal(messages[0].Id, d.MessageId));
		Assert.All(deliveries, d => Assert.Equal(AlertDeliveryStatus.Queued, d.Status));
	}

	[Fact]
	public async Task SecondPassOnTheSameLocalDay_QueuesNothingMore()
	{
		await fixture.ResetAsync();
		fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2026-09-16T06:00:00Z"));

		await SeedPendingDigestAsync(2, sendAt: new TimeOnly(9, 0));

		Assert.Equal(1, await BuildDigestsAsync());
		Assert.Equal(0, await BuildDigestsAsync());

		fixture.Clock.Advance(TimeSpan.FromHours(3));
		Assert.Equal(0, await BuildDigestsAsync());

		await using var verify = fixture.CreateScope();
		var db = verify.ServiceProvider.GetRequiredService<JobsDbContext>();
		Assert.Equal(1, await db.AlertMessages.CountAsync(m => m.Kind == AlertMessageKind.DailyDigest));
	}

	[Fact]
	public async Task TheNextLocalDay_QueuesAFreshDigest()
	{
		await fixture.ResetAsync();
		fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2026-09-16T06:00:00Z"));

		await SeedPendingDigestAsync(1, sendAt: new TimeOnly(9, 0), firstJobId: 300);
		Assert.Equal(1, await BuildDigestsAsync());

		fixture.Clock.Advance(TimeSpan.FromDays(1));

		// A new match arrives the following day.
		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			await AlertsTestData.AddJobAsync(
				db,
				AlertsTestData.NewJob(350, firstSeenAt: fixture.Clock.GetUtcNow()),
				"it");
			AlertsTestData.AddJobEvent(db, 350);
			await db.SaveChangesAsync();
		}

		await DrainAsync();

		Assert.Equal(1, await BuildDigestsAsync());

		await using var verify = fixture.CreateScope();
		var verifyDb = verify.ServiceProvider.GetRequiredService<JobsDbContext>();
		Assert.Equal(2, await verifyDb.AlertMessages.CountAsync(m => m.Kind == AlertMessageKind.DailyDigest));
	}

	[Fact]
	public async Task SubscribersInDifferentTimezones_FireAtDifferentInstants()
	{
		await fixture.ResetAsync();
		// 06:00 UTC is 10:00 in Tbilisi but 18:00 in Auckland, so a 12:00 local send time is
		// already past for one and still ahead for the other.
		fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2026-09-16T06:00:00Z"));

		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();

			await AlertsTestData.AddJobAsync(
				db,
				AlertsTestData.NewJob(400, firstSeenAt: fixture.Clock.GetUtcNow()),
				"it");

			await AlertsTestData.AddConfirmedSubscriptionAsync(
				db,
				new AlertCriteria(TitleContains: "backend"),
				cadence: AlertCadence.DailyDigest,
				address: "auckland@example.com",
				timeZoneId: "Pacific/Auckland",
				digestSendLocalTime: new TimeOnly(12, 0),
				matchFromAt: fixture.Clock.GetUtcNow().AddDays(-1));

			await AlertsTestData.AddConfirmedSubscriptionAsync(
				db,
				new AlertCriteria(TitleContains: "backend"),
				cadence: AlertCadence.DailyDigest,
				address: "tbilisi@example.com",
				timeZoneId: "Asia/Tbilisi",
				digestSendLocalTime: new TimeOnly(12, 0),
				matchFromAt: fixture.Clock.GetUtcNow().AddDays(-1));

			AlertsTestData.AddJobEvent(db, 400);
			await db.SaveChangesAsync();
		}

		await DrainAsync();

		// Auckland has passed 12:00 local; Tbilisi has not.
		Assert.Equal(1, await BuildDigestsAsync());

		await using var verify = fixture.CreateScope();
		var db2 = verify.ServiceProvider.GetRequiredService<JobsDbContext>();
		var queued = await db2.AlertMessages
			.Where(m => m.Kind == AlertMessageKind.DailyDigest)
			.Select(m => m.Address)
			.ToListAsync();

		Assert.Equal(["auckland@example.com"], queued);
	}

	[Fact]
	public async Task ItemsBeyondTheRenderCap_AreClosedOutRatherThanLeftPending()
	{
		await fixture.ResetAsync();
		fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2026-09-16T06:00:00Z"));

		var cap = fixture.Resolve<Microsoft.Extensions.Options.IOptions<Configuration.AlertOptions>>()
			.Value.Email.MaxDigestItems;

		var subscriptionId = await SeedPendingDigestAsync(cap + 5, sendAt: new TimeOnly(9, 0), firstJobId: 500);

		Assert.Equal(1, await BuildDigestsAsync());

		await using var verify = fixture.CreateScope();
		var db = verify.ServiceProvider.GetRequiredService<JobsDbContext>();

		var deliveries = await db.AlertDeliveries
			.Where(d => d.SubscriptionId == subscriptionId)
			.ToListAsync();

		Assert.Equal(cap, deliveries.Count(d => d.Status == AlertDeliveryStatus.Queued));
		Assert.Equal(5, deliveries.Count(d => d.Status == AlertDeliveryStatus.Skipped));
		Assert.All(
			deliveries.Where(d => d.Status == AlertDeliveryStatus.Skipped),
			d => Assert.Equal(AlertDeliverySkipReason.DigestOverflow, d.SkipReason));

		// Nothing is left pending, so the backlog cannot snowball into tomorrow forever.
		Assert.Empty(deliveries.Where(d => d.MessageId is null));
	}
}
