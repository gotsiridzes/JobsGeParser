using JobsGeParser.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobsGeParser.IntegrationTests;

/// <summary>
/// The outbox write lives inside the scrape write path, so these prove the one place where the
/// alert feature touches scraping behaves exactly as intended, including when it must stay quiet.
/// </summary>
[Collection("Alerts")]
public class OutboxEmitTests(AlertsFixture fixture)
{
	[Fact]
	public async Task FirstDetailFetch_EmitsExactlyOneJobReady()
	{
		await fixture.ResetAsync();

		await using var scope = fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
		var repo = scope.ServiceProvider.GetRequiredService<Repo>();

		var job = AlertsTestData.NewJob(1, description: null);
		job.DetailsFetchedAt = null;
		await AlertsTestData.AddJobAsync(db, job);

		await repo.UpsertDescriptionAsync(1, "A .NET role in Tbilisi.", "<p>A .NET role in Tbilisi.</p>");

		var events = await db.JobEvents.AsNoTracking().Where(e => e.JobId == 1).ToListAsync();

		Assert.Single(events);
		Assert.Equal(JobEventType.JobReady, events[0].EventType);
	}

	[Fact]
	public async Task RepeatedDetailFetchWithTheSameText_EmitsNothingFurther()
	{
		await fixture.ResetAsync();

		await using var scope = fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
		var repo = scope.ServiceProvider.GetRequiredService<Repo>();

		var job = AlertsTestData.NewJob(2, description: null);
		job.DetailsFetchedAt = null;
		await AlertsTestData.AddJobAsync(db, job);

		await repo.UpsertDescriptionAsync(2, "Same text.", null);
		await repo.UpsertDescriptionAsync(2, "Same text.", null);

		Assert.Equal(1, await db.JobEvents.CountAsync(e => e.JobId == 2));
	}

	[Fact]
	public async Task ChangedDescriptionOnACompleteJob_EmitsJobReEnriched()
	{
		await fixture.ResetAsync();

		await using var scope = fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
		var repo = scope.ServiceProvider.GetRequiredService<Repo>();

		var job = AlertsTestData.NewJob(3, description: null);
		job.DetailsFetchedAt = null;
		await AlertsTestData.AddJobAsync(db, job);

		await repo.UpsertDescriptionAsync(3, "First text.", null);
		await repo.UpsertDescriptionAsync(3, "The posting was edited.", null);

		var types = await db.JobEvents.AsNoTracking()
			.Where(e => e.JobId == 3)
			.OrderBy(e => e.Id)
			.Select(e => e.EventType)
			.ToListAsync();

		Assert.Equal([JobEventType.JobReady, JobEventType.JobReEnriched], types);
	}

	[Fact]
	public async Task EnrichmentBackfill_EmitsNothing()
	{
		await fixture.ResetAsync();

		await using var scope = fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
		var repo = scope.ServiceProvider.GetRequiredService<Repo>();

		var job = AlertsTestData.NewJob(4);
		job.EnrichmentVersion = 0;
		await AlertsTestData.AddJobAsync(db, job);

		await repo.BackfillEnrichmentAsync(100);

		// Bumping the extractor version must never alert everyone about the whole corpus.
		Assert.Equal(0, await db.JobEvents.CountAsync());
	}

	[Fact]
	public async Task LateCategoryLinkOnACompleteJob_EmitsJobCategoryLinked()
	{
		await fixture.ResetAsync();

		await using var scope = fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
		var repo = scope.ServiceProvider.GetRequiredService<Repo>();

		await AlertsTestData.AddJobAsync(db, AlertsTestData.NewJob(5), "it");

		await repo.LinkJobToCategoryAsync(5, "finance-statistics");

		var events = await db.JobEvents.AsNoTracking().Where(e => e.JobId == 5).ToListAsync();

		Assert.Single(events);
		Assert.Equal(JobEventType.JobCategoryLinked, events[0].EventType);
		Assert.Equal("finance-statistics", events[0].CategorySlug);
	}

	[Fact]
	public async Task RelinkingAnExistingCategory_EmitsNothing()
	{
		await fixture.ResetAsync();

		await using var scope = fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
		var repo = scope.ServiceProvider.GetRequiredService<Repo>();

		await AlertsTestData.AddJobAsync(db, AlertsTestData.NewJob(6), "it");

		await repo.LinkJobToCategoryAsync(6, "it");

		Assert.Equal(0, await db.JobEvents.CountAsync(e => e.JobId == 6));
	}

	[Fact]
	public async Task JobAndEvent_ArePersistedInOneTransaction()
	{
		await fixture.ResetAsync();

		await using var scope = fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
		var repo = scope.ServiceProvider.GetRequiredService<Repo>();

		var job = AlertsTestData.NewJob(7, description: null);
		job.DetailsFetchedAt = null;
		await AlertsTestData.AddJobAsync(db, job);

		await repo.UpsertDescriptionAsync(7, "Committed together.", null);

		// Read on a fresh context: both the description and the event must be visible, or neither.
		await using var verifyScope = fixture.CreateScope();
		var verifyDb = verifyScope.ServiceProvider.GetRequiredService<JobsDbContext>();

		var stored = await verifyDb.Jobs.AsNoTracking().FirstAsync(j => j.Id == 7);
		var eventCount = await verifyDb.JobEvents.CountAsync(e => e.JobId == 7);

		Assert.Equal("Committed together.", stored.Description);
		Assert.Equal(1, eventCount);
	}
}
