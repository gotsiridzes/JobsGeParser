using JobsGeParser.Data;
using Microsoft.EntityFrameworkCore;

namespace JobsGeParser;

public class Repo
{
	private readonly JobsDbContext _db;

	public Repo(JobsDbContext db) => _db = db;

	public async Task<JobUpsertResult> UpsertAsync(JobApplication job, CancellationToken ct = default)
	{
		var now = DateTimeOffset.UtcNow;
		var existing = await _db.Jobs.FindAsync([job.Id], ct);

		if (existing is null)
		{
			_db.Jobs.Add(MapToEntity(job, now, now, null));
			await _db.SaveChangesAsync(ct);
			return JobUpsertResult.Inserted;
		}

		if (HasSameContent(existing, job))
		{
			existing.LastSeenAt = now;
			await _db.SaveChangesAsync(ct);
			return JobUpsertResult.Skipped;
		}

		existing.Name = job.Name;
		existing.Link = job.Link;
		existing.Company = job.Company;
		existing.CompanyLink = job.CompanyLink;
		existing.Published = job.Published;
		existing.EndDate = job.EndDate;
		existing.Description = job.Description;
		existing.LastSeenAt = now;
		existing.UpdatedAt = now;
		await _db.SaveChangesAsync(ct);
		return JobUpsertResult.Updated;
	}

	public async Task<IReadOnlyList<JobApplication>> GetProcessedApplicationsAsync(CancellationToken ct = default)
	{
		var entities = await _db.Jobs
			.OrderByDescending(j => j.LastSeenAt)
			.ToListAsync(ct);

		return entities.Select(MapToDomain).ToList();
	}

	public async Task<IReadOnlyList<JobApplication>> ListDotnetApplicationsAsync(CancellationToken ct = default)
	{
		var entities = await _db.Jobs
			.Where(j => EF.Functions.ILike(j.Name, "%.net%"))
			.OrderByDescending(j => j.LastSeenAt)
			.ToListAsync(ct);

		return entities.Select(MapToDomain).ToList();
	}

	public async Task<ScrapeRunEntity> StartScrapeRunAsync(CancellationToken ct = default)
	{
		var run = new ScrapeRunEntity
		{
			StartedAt = DateTimeOffset.UtcNow,
			Status = ScrapeRunStatus.Running
		};

		_db.ScrapeRuns.Add(run);
		await _db.SaveChangesAsync(ct);
		return run;
	}

	public async Task UpdateScrapeRunProgressAsync(
		long runId,
		int inserted,
		int updated,
		int skipped,
		int failed,
		CancellationToken ct = default)
	{
		var run = await _db.ScrapeRuns.FindAsync([runId], ct);
		if (run is null || run.Status != ScrapeRunStatus.Running)
			return;

		run.Inserted = inserted;
		run.Updated = updated;
		run.Skipped = skipped;
		run.Failed = failed;
		await _db.SaveChangesAsync(ct);
	}

	public async Task CompleteScrapeRunAsync(long runId, ScrapeResult result, CancellationToken ct = default)
	{
		var run = await _db.ScrapeRuns.FindAsync([runId], ct)
			?? throw new InvalidOperationException($"Scrape run {runId} not found.");

		run.FinishedAt = DateTimeOffset.UtcNow;
		run.Inserted = result.Inserted;
		run.Updated = result.Updated;
		run.Skipped = result.Skipped;
		run.Failed = result.Failed;
		run.Status = ScrapeRunStatus.Completed;
		await _db.SaveChangesAsync(ct);
	}

	public async Task FailScrapeRunAsync(long runId, string errorMessage, CancellationToken ct = default)
	{
		var run = await _db.ScrapeRuns.FindAsync([runId], ct);
		if (run is null)
			return;

		run.FinishedAt = DateTimeOffset.UtcNow;
		run.Status = ScrapeRunStatus.Failed;
		run.ErrorMessage = errorMessage;
		await _db.SaveChangesAsync(ct);
	}

	public async Task<ScrapeRunEntity?> GetLatestScrapeRunAsync(CancellationToken ct = default) =>
		await _db.ScrapeRuns
			.OrderByDescending(r => r.StartedAt)
			.FirstOrDefaultAsync(ct);

	private static bool HasSameContent(JobEntity existing, JobApplication job) =>
		existing.Name == job.Name
		&& existing.Link == job.Link
		&& existing.Company == job.Company
		&& existing.CompanyLink == job.CompanyLink
		&& existing.Published == job.Published
		&& existing.EndDate == job.EndDate
		&& existing.Description == job.Description;

	private static JobEntity MapToEntity(
		JobApplication job,
		DateTimeOffset firstSeenAt,
		DateTimeOffset lastSeenAt,
		DateTimeOffset? updatedAt) =>
		new()
		{
			Id = job.Id,
			Name = job.Name,
			Link = job.Link,
			Company = job.Company,
			CompanyLink = job.CompanyLink,
			Published = job.Published,
			EndDate = job.EndDate,
			Description = job.Description,
			FirstSeenAt = firstSeenAt,
			LastSeenAt = lastSeenAt,
			UpdatedAt = updatedAt
		};

	private static JobApplication MapToDomain(JobEntity entity)
	{
		var application = new JobApplication(
			entity.Id,
			entity.Name,
			entity.Company,
			entity.Published,
			entity.EndDate);

		application.SetLink(entity.Link);
		application.SetCompanyLink(entity.CompanyLink);
		application.SetDescription(entity.Description ?? string.Empty);
		return application;
	}
}
