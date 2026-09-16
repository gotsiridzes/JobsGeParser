using JobsGeParser.Alerts;
using JobsGeParser.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobsGeParser.IntegrationTests;

/// <summary>
/// The rule: a person never receives the same job twice, whatever mix of alerts matched it.
/// Per-alert dedup is the unique index; these cover the cross-alert cases.
/// </summary>
[Collection("Alerts")]
public class AlertPerPersonDedupTests(AlertsFixture fixture)
{
	private Task<int> DrainAsync() => fixture.Resolve<AlertOutboxDrainer>().DrainOnceAsync();

	private Task<int> PumpAsync() => fixture.Resolve<AlertSendPump>().PumpOnceAsync();

	private Task<int> BuildDigestsAsync() => fixture.Resolve<AlertDigestBuilder>().BuildDueDigestsAsync();

	private static async Task<AlertSubscriptionEntity> AddAlertAsync(
		JobsDbContext db,
		Guid subscriberId,
		AlertCriteria criteria,
		AlertCadence cadence,
		DateTimeOffset matchFromAt)
	{
		var canonical = criteria.Canonicalize();
		var subscription = new AlertSubscriptionEntity
		{
			Id = Guid.CreateVersion7(),
			SubscriberId = subscriberId,
			Name = "Second alert",
			Status = AlertSubscriptionStatus.Active,
			Cadence = cadence,
			DigestSendLocalTime = new TimeOnly(9, 0),
			MatchFromAt = matchFromAt,
			CriteriaHash = canonical.ComputeHash(),
			CreatedAt = matchFromAt.AddSeconds(1),
			UpdatedAt = matchFromAt,
			Query = canonical.Query,
			TitleContains = canonical.TitleContains,
			CompanyContains = canonical.CompanyContains
		};

		db.AlertSubscriptions.Add(subscription);
		await db.SaveChangesAsync();
		return subscription;
	}

	[Fact]
	public async Task TwoInstantAlertsMatchingOneJob_SendOneMessage()
	{
		await fixture.ResetAsync();
		fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2026-09-16T06:00:00Z"));
		var since = fixture.Clock.GetUtcNow().AddDays(-1);

		Guid first, second;
		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			await AlertsTestData.AddJobAsync(db, AlertsTestData.NewJob(700, firstSeenAt: fixture.Clock.GetUtcNow()), "it");

			var (subscriber, a) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db, new AlertCriteria(TitleContains: "backend"), matchFromAt: since);
			var b = await AddAlertAsync(db, subscriber.Id, new AlertCriteria(CompanyContains: "acme"), AlertCadence.Instant, since);

			first = a.Id;
			second = b.Id;

			AlertsTestData.AddJobEvent(db, 700);
			await db.SaveChangesAsync();
		}

		await DrainAsync();
		await PumpAsync();

		await using var verify = fixture.CreateScope();
		var vdb = verify.ServiceProvider.GetRequiredService<JobsDbContext>();

		Assert.Single(fixture.Email.Sent);

		var deliveries = await vdb.AlertDeliveries.AsNoTracking().ToListAsync();
		Assert.Equal(AlertDeliveryStatus.Sent, deliveries.Single(d => d.SubscriptionId == first).Status);

		var skipped = deliveries.Single(d => d.SubscriptionId == second);
		Assert.Equal(AlertDeliveryStatus.Skipped, skipped.Status);
		Assert.Equal(AlertDeliverySkipReason.AlreadySent, skipped.SkipReason);
	}

	[Fact]
	public async Task JobSentInstantly_IsLeftOutOfAnotherAlertsDigest()
	{
		await fixture.ResetAsync();
		fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2026-09-16T06:00:00Z"));
		var since = fixture.Clock.GetUtcNow().AddDays(-1);

		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			await AlertsTestData.AddJobAsync(db, AlertsTestData.NewJob(710, firstSeenAt: fixture.Clock.GetUtcNow()), "it");

			var (subscriber, _) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db, new AlertCriteria(TitleContains: "backend"), matchFromAt: since);
			await AddAlertAsync(db, subscriber.Id, new AlertCriteria(CompanyContains: "acme"), AlertCadence.DailyDigest, since);

			AlertsTestData.AddJobEvent(db, 710);
			await db.SaveChangesAsync();
		}

		await DrainAsync();
		await PumpAsync();

		// 06:00 UTC is 10:00 in Tbilisi, past the 09:00 digest time.
		Assert.Equal(0, await BuildDigestsAsync());
		Assert.Single(fixture.Email.Sent);

		await using var verify = fixture.CreateScope();
		var vdb = verify.ServiceProvider.GetRequiredService<JobsDbContext>();
		Assert.Equal(0, await vdb.AlertMessages.CountAsync(m => m.Kind == AlertMessageKind.DailyDigest));
	}

	[Fact]
	public async Task SwitchingAnInstantAlertToDigest_DoesNotResendItsJobs()
	{
		await fixture.ResetAsync();
		fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2026-09-16T06:00:00Z"));

		Guid subscriptionId;
		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			await AlertsTestData.AddJobAsync(db, AlertsTestData.NewJob(720, firstSeenAt: fixture.Clock.GetUtcNow()), "it");

			var (_, subscription) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db, new AlertCriteria(TitleContains: "backend"), matchFromAt: fixture.Clock.GetUtcNow().AddDays(-1));
			subscriptionId = subscription.Id;

			AlertsTestData.AddJobEvent(db, 720);
			await db.SaveChangesAsync();
		}

		await DrainAsync();
		await PumpAsync();

		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			var subscription = await db.AlertSubscriptions.SingleAsync(s => s.Id == subscriptionId);
			subscription.Cadence = AlertCadence.DailyDigest;
			await db.SaveChangesAsync();
		}

		Assert.Equal(0, await BuildDigestsAsync());
		Assert.Single(fixture.Email.Sent);
	}

	[Fact]
	public async Task TwoDigestAlertsSharingAJob_ListItOnlyOnce()
	{
		await fixture.ResetAsync();
		fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2026-09-16T06:00:00Z"));
		var since = fixture.Clock.GetUtcNow().AddDays(-1);

		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			await AlertsTestData.AddJobAsync(db, AlertsTestData.NewJob(730, firstSeenAt: fixture.Clock.GetUtcNow()), "it");

			var (subscriber, _) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db, new AlertCriteria(TitleContains: "backend"), cadence: AlertCadence.DailyDigest, matchFromAt: since);
			await AddAlertAsync(db, subscriber.Id, new AlertCriteria(CompanyContains: "acme"), AlertCadence.DailyDigest, since);

			AlertsTestData.AddJobEvent(db, 730);
			await db.SaveChangesAsync();
		}

		await DrainAsync();

		// The first digest queues the job, the second sees it as already queued and stays silent.
		Assert.Equal(1, await BuildDigestsAsync());

		await using var verify = fixture.CreateScope();
		var vdb = verify.ServiceProvider.GetRequiredService<JobsDbContext>();
		Assert.Equal(1, await vdb.AlertMessages.CountAsync(m => m.Kind == AlertMessageKind.DailyDigest));
		Assert.Equal(1, await vdb.AlertDeliveries.CountAsync(d => d.SkipReason == AlertDeliverySkipReason.AlreadySent));
	}

	[Fact]
	public async Task DeletedAlertsHistory_StillCounts()
	{
		await fixture.ResetAsync();
		fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2026-09-16T06:00:00Z"));
		var since = fixture.Clock.GetUtcNow().AddDays(-1);

		Guid subscriberId;
		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			await AlertsTestData.AddJobAsync(db, AlertsTestData.NewJob(740, firstSeenAt: fixture.Clock.GetUtcNow()), "it");

			var (subscriber, _) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db, new AlertCriteria(TitleContains: "backend"), matchFromAt: since);
			subscriberId = subscriber.Id;

			AlertsTestData.AddJobEvent(db, 740);
			await db.SaveChangesAsync();
		}

		await DrainAsync();
		await PumpAsync();

		// Delete the alert, create a new one that also matches, and replay the job.
		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			foreach (var s in db.AlertSubscriptions.Where(s => s.SubscriberId == subscriberId))
				s.Status = AlertSubscriptionStatus.Deleted;

			await AddAlertAsync(db, subscriberId, new AlertCriteria(CompanyContains: "acme"), AlertCadence.Instant, since);
			AlertsTestData.AddJobEvent(db, 740, JobEventType.JobReEnriched);
			await db.SaveChangesAsync();
		}

		await DrainAsync();
		await PumpAsync();

		Assert.Single(fixture.Email.Sent);
	}

	[Fact]
	public async Task SentMessage_CountsOnTheManagePage()
	{
		await fixture.ResetAsync();
		fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2026-09-16T06:00:00Z"));

		Guid subscriberId, subscriptionId;
		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			await AlertsTestData.AddJobAsync(db, AlertsTestData.NewJob(750, firstSeenAt: fixture.Clock.GetUtcNow()), "it");
			var (subscriber, subscription) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db, new AlertCriteria(TitleContains: "backend"), matchFromAt: fixture.Clock.GetUtcNow().AddDays(-1));
			subscriberId = subscriber.Id;
			subscriptionId = subscription.Id;

			AlertsTestData.AddJobEvent(db, 750);
			await db.SaveChangesAsync();
		}

		await DrainAsync();
		await PumpAsync();

		await using var verify = fixture.CreateScope();
		var repo = verify.ServiceProvider.GetRequiredService<AlertRepo>();
		var stats = await repo.GetSubscriptionStatsAsync(subscriberId);

		Assert.Equal(1, stats[subscriptionId].Sent);
	}
}
