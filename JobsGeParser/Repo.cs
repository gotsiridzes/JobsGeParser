using JobsGeParser.Data;
using JobsGeParser.Workers;
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

	public async Task LinkJobToCategoryAsync(int jobId, string categorySlug, CancellationToken ct = default)
	{
		var now = DateTimeOffset.UtcNow;
		var link = await _db.JobCategories.FindAsync([jobId, categorySlug], ct);

		if (link is null)
		{
			_db.JobCategories.Add(new JobCategoryEntity
			{
				JobId = jobId,
				CategorySlug = categorySlug,
				FirstSeenAt = now,
				LastSeenAt = now
			});
		}
		else
		{
			link.LastSeenAt = now;
		}

		await _db.SaveChangesAsync(ct);
	}

	public async Task<JobUpsertResult> UpsertAndLinkCategoryAsync(
		JobApplication job,
		string categorySlug,
		CancellationToken ct = default)
	{
		var now = DateTimeOffset.UtcNow;
		var existing = await _db.Jobs.FindAsync([job.Id], ct);
		JobUpsertResult result;

		if (existing is null)
		{
			_db.Jobs.Add(MapToEntity(job, now, now, null));
			result = JobUpsertResult.Inserted;
		}
		else if (HasSameContent(existing, job))
		{
			existing.LastSeenAt = now;
			result = JobUpsertResult.Skipped;
		}
		else
		{
			existing.Name = job.Name;
			existing.Link = job.Link;
			existing.Company = job.Company;
			existing.CompanyLink = job.CompanyLink;
			existing.Published = job.Published;
			existing.EndDate = job.EndDate;
			existing.Description = job.Description;
			existing.LastSeenAt = now;
			existing.UpdatedAt = now;
			result = JobUpsertResult.Updated;
		}

		var link = await _db.JobCategories.FindAsync([job.Id, categorySlug], ct);
		if (link is null)
		{
			_db.JobCategories.Add(new JobCategoryEntity
			{
				JobId = job.Id,
				CategorySlug = categorySlug,
				FirstSeenAt = now,
				LastSeenAt = now
			});
		}
		else
		{
			link.LastSeenAt = now;
		}

		await _db.SaveChangesAsync(ct);
		return result;
	}

	public async Task<IReadOnlyList<JobApplication>> GetJobsAsync(string? categorySlug = null, CancellationToken ct = default)
	{
		var query = _db.Jobs.AsQueryable();

		if (!string.IsNullOrWhiteSpace(categorySlug))
		{
			query = query.Where(j => j.JobCategories.Any(jc => jc.CategorySlug == categorySlug));
		}

		var entities = await query
			.OrderByDescending(j => j.LastSeenAt)
			.ToListAsync(ct);

		return entities.Select(MapToDomain).ToList();
	}

	public async Task<IReadOnlyList<JobApplication>> ListDotnetApplicationsAsync(
		string? categorySlug = null,
		CancellationToken ct = default)
	{
		var query = _db.Jobs.Where(j => EF.Functions.ILike(j.Name, "%.net%"));

		if (!string.IsNullOrWhiteSpace(categorySlug))
		{
			query = query.Where(j => j.JobCategories.Any(jc => jc.CategorySlug == categorySlug));
		}

		var entities = await query
			.OrderByDescending(j => j.LastSeenAt)
			.ToListAsync(ct);

		return entities.Select(MapToDomain).ToList();
	}

	public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
	{
		var categories = await _db.Categories
			.AsNoTracking()
			.OrderBy(c => c.Name)
			.ToListAsync(ct);

		var jobCounts = await _db.JobCategories
			.GroupBy(jc => jc.CategorySlug)
			.Select(g => new { g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.Key, x => x.Count, ct);

		var latestRunIds = await _db.ScrapeRuns
			.AsNoTracking()
			.Where(r => r.CategorySlug != null)
			.GroupBy(r => r.CategorySlug!)
			.Select(g => g.OrderByDescending(r => r.StartedAt).Select(r => r.Id).First())
			.ToListAsync(ct);

		var latestRuns = await _db.ScrapeRuns
			.AsNoTracking()
			.Where(r => latestRunIds.Contains(r.Id))
			.ToDictionaryAsync(r => r.CategorySlug!, ct);

		return categories
			.Select(c => new CategoryDto(
				c.Slug,
				c.Name,
				c.ListUrl,
				c.Enabled,
				jobCounts.GetValueOrDefault(c.Slug),
				latestRuns.GetValueOrDefault(c.Slug)))
			.ToList();
	}

	public async Task<ScrapeRunEntity> StartScrapeRunAsync(
		string categorySlug,
		Guid batchId,
		CancellationToken ct = default)
	{
		var run = new ScrapeRunEntity
		{
			BatchId = batchId,
			CategorySlug = categorySlug,
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

	public async Task<ScrapeRunEntity?> GetScrapeRunByIdAsync(long id, CancellationToken ct = default) =>
		await _db.ScrapeRuns
			.AsNoTracking()
			.FirstOrDefaultAsync(r => r.Id == id, ct);

	public async Task<IReadOnlyList<ScrapeRunEntity>> GetActiveScrapeRunsAsync(CancellationToken ct = default) =>
		await _db.ScrapeRuns
			.AsNoTracking()
			.Where(r => r.Status == ScrapeRunStatus.Running)
			.OrderByDescending(r => r.StartedAt)
			.ToListAsync(ct);

	public async Task<ScrapeRunsPageDto> GetScrapeRunsAsync(
		string? status = null,
		string? categorySlug = null,
		Guid? batchId = null,
		int limit = 50,
		int offset = 0,
		CancellationToken ct = default)
	{
		limit = Math.Clamp(limit, 1, 200);
		offset = Math.Max(offset, 0);

		var query = _db.ScrapeRuns.AsNoTracking();

		if (!string.IsNullOrWhiteSpace(status))
			query = query.Where(r => r.Status == status);

		if (!string.IsNullOrWhiteSpace(categorySlug))
			query = query.Where(r => r.CategorySlug == categorySlug);

		if (batchId is not null)
			query = query.Where(r => r.BatchId == batchId);

		var totalCount = await query.CountAsync(ct);

		var items = await query
			.OrderByDescending(r => r.StartedAt)
			.Skip(offset)
			.Take(limit)
			.ToListAsync(ct);

		return new ScrapeRunsPageDto(items, totalCount, limit, offset);
	}

	public async Task<IReadOnlyList<ScrapeBatchSummaryDto>> GetRecentBatchesAsync(
		int limit = 20,
		CancellationToken ct = default)
	{
		limit = Math.Clamp(limit, 1, 100);

		var batchIds = await _db.ScrapeRuns
			.AsNoTracking()
			.Where(r => r.BatchId != null)
			.GroupBy(r => r.BatchId)
			.OrderByDescending(g => g.Max(r => r.StartedAt))
			.Take(limit)
			.Select(g => g.Key!.Value)
			.ToListAsync(ct);

		var summaries = new List<ScrapeBatchSummaryDto>();
		foreach (var id in batchIds)
		{
			var runs = await _db.ScrapeRuns
				.AsNoTracking()
				.Where(r => r.BatchId == id)
				.OrderBy(r => r.StartedAt)
				.ToListAsync(ct);

			if (runs.Count == 0)
				continue;

			summaries.Add(new ScrapeBatchSummaryDto(
				id,
				runs.Min(r => r.StartedAt),
				runs.All(r => r.FinishedAt is not null) ? runs.Max(r => r.FinishedAt) : null,
				runs.Count,
				runs.Count(r => r.Status == ScrapeRunStatus.Running),
				runs.Count(r => r.Status == ScrapeRunStatus.Completed),
				runs.Count(r => r.Status == ScrapeRunStatus.Failed),
				runs));
		}

		return summaries;
	}

	public async Task<ScrapeOverviewDto> GetScrapeOverviewAsync(
		JobsGeParserOptions options,
		ScrapeWorkerSnapshot workerSnapshot,
		CancellationToken ct = default)
	{
		var worker = new ScrapeWorkerStatusDto(
			options.ScrapeEnabled,
			options.ScrapeIntervalMinutes,
			options.ScrapeOnStartup,
			workerSnapshot);

		var activeRuns = await GetActiveScrapeRunsAsync(ct);
		var latestPerCategory = await GetLatestScrapeRunsPerCategoryAsync(ct);
		var recentRuns = (await GetScrapeRunsAsync(limit: 20, offset: 0, ct: ct)).Items;
		var recentBatches = await GetRecentBatchesAsync(limit: 10, ct);

		return new ScrapeOverviewDto(
			worker,
			activeRuns,
			latestPerCategory,
			recentRuns,
			recentBatches);
	}

	public async Task<ScrapeRunEntity?> GetLatestScrapeRunAsync(string categorySlug, CancellationToken ct = default) =>
		await _db.ScrapeRuns
			.Where(r => r.CategorySlug == categorySlug)
			.OrderByDescending(r => r.StartedAt)
			.FirstOrDefaultAsync(ct);

	public async Task<IReadOnlyList<ScrapeRunEntity>> GetLatestScrapeRunsPerCategoryAsync(CancellationToken ct = default)
	{
		var enabledSlugs = await _db.Categories
			.Where(c => c.Enabled)
			.Select(c => c.Slug)
			.ToListAsync(ct);

		var results = new List<ScrapeRunEntity>();
		foreach (var slug in enabledSlugs)
		{
			var run = await GetLatestScrapeRunAsync(slug, ct);
			if (run is not null)
				results.Add(run);
		}

		return results;
	}

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
