using JobsGeParser.Alerts;
using JobsGeParser.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobsGeParser.IntegrationTests;

/// <summary>
/// The reverse match and the dedup ledger. None of this can be proven without a database: the
/// prefilter is raw SQL, dedup is a Postgres constraint, and full text matching only exists as
/// a tsquery.
/// </summary>
[Collection("Alerts")]
public class AlertMatchingTests(AlertsFixture fixture)
{
	private async Task<int> DrainAsync() =>
		await fixture.Resolve<AlertOutboxDrainer>().DrainOnceAsync();

	[Fact]
	public async Task MatchingJob_ProducesOneDeliveryAndOneInstantMessage()
	{
		await fixture.ResetAsync();

		Guid subscriptionId;
		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			await AlertsTestData.AddJobAsync(db, AlertsTestData.NewJob(10), "it");
			var (_, subscription) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db,
				new AlertCriteria(TitleContains: "backend"));
			subscriptionId = subscription.Id;

			AlertsTestData.AddJobEvent(db, 10);
			await db.SaveChangesAsync();
		}

		await DrainAsync();

		await using var verify = fixture.CreateScope();
		var verifyDb = verify.ServiceProvider.GetRequiredService<JobsDbContext>();

		Assert.Equal(1, await AlertsTestData.CountDeliveriesAsync(verifyDb, subscriptionId));
		Assert.Equal(1, await verifyDb.AlertMessages.CountAsync(m => m.Kind == AlertMessageKind.InstantMatch));
	}

	[Fact]
	public async Task DrainingTheSameEventTwice_StillProducesOneDelivery()
	{
		await fixture.ResetAsync();

		Guid subscriptionId;
		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			await AlertsTestData.AddJobAsync(db, AlertsTestData.NewJob(11), "it");
			var (_, subscription) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db,
				new AlertCriteria(TitleContains: "backend"));
			subscriptionId = subscription.Id;

			AlertsTestData.AddJobEvent(db, 11);
			AlertsTestData.AddJobEvent(db, 11, JobEventType.JobReEnriched);
			await db.SaveChangesAsync();
		}

		await DrainAsync();
		await DrainAsync();

		await using var verify = fixture.CreateScope();
		var verifyDb = verify.ServiceProvider.GetRequiredService<JobsDbContext>();

		// The unique index is the whole guarantee; a replay must be a no-op.
		Assert.Equal(1, await AlertsTestData.CountDeliveriesAsync(verifyDb, subscriptionId));
		Assert.Equal(1, await verifyDb.AlertMessages.CountAsync(m => m.Kind == AlertMessageKind.InstantMatch));
	}

	[Fact]
	public async Task ConcurrentDrains_ProcessEveryEventExactlyOnce()
	{
		await fixture.ResetAsync();

		Guid subscriptionId;
		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			var (_, subscription) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db,
				new AlertCriteria(TitleContains: "backend"));
			subscriptionId = subscription.Id;

			for (var id = 100; id < 140; id++)
			{
				await AlertsTestData.AddJobAsync(db, AlertsTestData.NewJob(id), "it");
				AlertsTestData.AddJobEvent(db, id);
			}

			await db.SaveChangesAsync();
		}

		// This is the SKIP LOCKED proof, and it cannot be unit tested.
		await Task.WhenAll(DrainAsync(), DrainAsync(), DrainAsync());

		await using var verify = fixture.CreateScope();
		var verifyDb = verify.ServiceProvider.GetRequiredService<JobsDbContext>();

		Assert.Equal(40, await AlertsTestData.CountDeliveriesAsync(verifyDb, subscriptionId));
		Assert.Equal(40, await verifyDb.AlertMessages.CountAsync(m => m.Kind == AlertMessageKind.InstantMatch));
		Assert.Equal(0, await verifyDb.JobEvents.CountAsync(e => e.ProcessedAt == null));
	}

	[Fact]
	public async Task FullTextQuery_MatchesTheSameJobsAsSearch()
	{
		await fixture.ResetAsync();

		Guid matching, notMatching;
		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();

			await AlertsTestData.AddJobAsync(
				db,
				AlertsTestData.NewJob(20, name: "Platform Engineer", description: "Backend developer wanted for our team."),
				"it");

			var (_, hit) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db,
				new AlertCriteria(Query: "backend developer"),
				address: "hit@example.com");

			var (_, miss) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db,
				new AlertCriteria(Query: "veterinary surgeon"),
				address: "miss@example.com");

			matching = hit.Id;
			notMatching = miss.Id;

			AlertsTestData.AddJobEvent(db, 20);
			await db.SaveChangesAsync();
		}

		await DrainAsync();

		await using var verify = fixture.CreateScope();
		var verifyDb = verify.ServiceProvider.GetRequiredService<JobsDbContext>();

		Assert.Equal(1, await AlertsTestData.CountDeliveriesAsync(verifyDb, matching));
		Assert.Equal(0, await AlertsTestData.CountDeliveriesAsync(verifyDb, notMatching));
	}

	[Fact]
	public async Task CategoryFilter_IsAppliedByThePrefilter()
	{
		await fixture.ResetAsync();

		Guid itOnly, financeOnly;
		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			await AlertsTestData.AddJobAsync(db, AlertsTestData.NewJob(21), "it");

			var (_, a) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db,
				new AlertCriteria(CategorySlugs: ["it"]),
				address: "it@example.com");

			var (_, b) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db,
				new AlertCriteria(CategorySlugs: ["finance-statistics"]),
				address: "finance@example.com");

			itOnly = a.Id;
			financeOnly = b.Id;

			AlertsTestData.AddJobEvent(db, 21);
			await db.SaveChangesAsync();
		}

		await DrainAsync();

		await using var verify = fixture.CreateScope();
		var verifyDb = verify.ServiceProvider.GetRequiredService<JobsDbContext>();

		Assert.Equal(1, await AlertsTestData.CountDeliveriesAsync(verifyDb, itOnly));
		Assert.Equal(0, await AlertsTestData.CountDeliveriesAsync(verifyDb, financeOnly));
	}

	[Fact]
	public async Task MatchFromAt_ExcludesJobsOlderThanTheSubscription()
	{
		await fixture.ResetAsync();

		Guid subscriptionId;
		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();

			var subscribedAt = DateTimeOffset.UtcNow;

			// Seen before they subscribed: they must not be told about it.
			await AlertsTestData.AddJobAsync(
				db,
				AlertsTestData.NewJob(30, firstSeenAt: subscribedAt.AddDays(-2)),
				"it");

			// Seen after: this one is theirs.
			await AlertsTestData.AddJobAsync(
				db,
				AlertsTestData.NewJob(31, firstSeenAt: subscribedAt.AddMinutes(5)),
				"it");

			var (_, subscription) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db,
				new AlertCriteria(TitleContains: "backend"),
				matchFromAt: subscribedAt);

			subscriptionId = subscription.Id;

			AlertsTestData.AddJobEvent(db, 30);
			AlertsTestData.AddJobEvent(db, 31);
			await db.SaveChangesAsync();
		}

		await DrainAsync();

		await using var verify = fixture.CreateScope();
		var verifyDb = verify.ServiceProvider.GetRequiredService<JobsDbContext>();

		var jobIds = await verifyDb.AlertDeliveries
			.Where(d => d.SubscriptionId == subscriptionId)
			.Select(d => d.JobId)
			.ToListAsync();

		Assert.Equal([31], jobIds);
	}

	[Fact]
	public async Task PausedSubscription_ReceivesNothing()
	{
		await fixture.ResetAsync();

		Guid subscriptionId;
		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			await AlertsTestData.AddJobAsync(db, AlertsTestData.NewJob(40), "it");

			var (_, subscription) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db,
				new AlertCriteria(TitleContains: "backend"));

			subscription.Status = AlertSubscriptionStatus.Paused;
			subscriptionId = subscription.Id;

			AlertsTestData.AddJobEvent(db, 40);
			await db.SaveChangesAsync();
		}

		await DrainAsync();

		await using var verify = fixture.CreateScope();
		var verifyDb = verify.ServiceProvider.GetRequiredService<JobsDbContext>();

		Assert.Equal(0, await AlertsTestData.CountDeliveriesAsync(verifyDb, subscriptionId));
	}

	[Fact]
	public async Task UnconfirmedSubscriber_ReceivesNothing()
	{
		await fixture.ResetAsync();

		Guid subscriptionId;
		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			await AlertsTestData.AddJobAsync(db, AlertsTestData.NewJob(41), "it");

			var (subscriber, subscription) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db,
				new AlertCriteria(TitleContains: "backend"));

			subscriber.Status = AlertSubscriberStatus.Pending;
			subscriptionId = subscription.Id;

			AlertsTestData.AddJobEvent(db, 41);
			await db.SaveChangesAsync();
		}

		await DrainAsync();

		await using var verify = fixture.CreateScope();
		var verifyDb = verify.ServiceProvider.GetRequiredService<JobsDbContext>();

		Assert.Equal(0, await AlertsTestData.CountDeliveriesAsync(verifyDb, subscriptionId));
	}

	[Fact]
	public async Task DigestSubscription_RecordsADeliveryButQueuesNoMessageYet()
	{
		await fixture.ResetAsync();

		Guid subscriptionId;
		await using (var scope = fixture.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
			await AlertsTestData.AddJobAsync(db, AlertsTestData.NewJob(50), "it");

			var (_, subscription) = await AlertsTestData.AddConfirmedSubscriptionAsync(
				db,
				new AlertCriteria(TitleContains: "backend"),
				cadence: AlertCadence.DailyDigest);

			subscriptionId = subscription.Id;

			AlertsTestData.AddJobEvent(db, 50);
			await db.SaveChangesAsync();
		}

		await DrainAsync();

		await using var verify = fixture.CreateScope();
		var verifyDb = verify.ServiceProvider.GetRequiredService<JobsDbContext>();

		// One matching path, two delivery paths: the match is recorded now, the message waits.
		Assert.Equal(1, await AlertsTestData.CountDeliveriesAsync(verifyDb, subscriptionId));
		Assert.Equal(0, await verifyDb.AlertMessages.CountAsync());
	}
}
