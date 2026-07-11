using JobsGeParser.Configuration;
using JobsGeParser.Endpoints.Dtos;
using JobsGeParser.Enrichment;
using JobsGeParser.Models;
using JobsGeParser.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace JobsGeParser.Data;

public class Repo(
	JobsDbContext db,
	IOptions<JobsGeParserOptions> options,
	EnrichmentService enrichment)
{
	private readonly JobsDbContext _db = db;
	private readonly JobsGeParserOptions _options = options.Value;
	private readonly EnrichmentService _enrichment = enrichment;
	private const double TrigramSimilarityThreshold = 0.3;

	public async Task<JobUpsertResult> UpsertAsync(JobApplication job, CancellationToken ct = default)
	{
		var now = DateTimeOffset.UtcNow;
		var existing = await _db.Jobs.FindAsync([job.Id], ct);

		if (existing is null)
		{
			_db.Jobs.Add(MapToEntity(job, now, now, null, job.Description is not null ? now : null));
			await _db.SaveChangesAsync(ct);
			return JobUpsertResult.Inserted;
		}

		if (HasSameContent(existing, job))
		{
			existing.LastSeenAt = now;
			await _db.SaveChangesAsync(ct);
			return JobUpsertResult.Skipped;
		}

		ApplyMetadata(existing, job);
		existing.Description = job.Description;
		if (job.Description is not null)
			existing.DetailsFetchedAt = now;
		existing.LastSeenAt = now;
		existing.UpdatedAt = now;
		await _db.SaveChangesAsync(ct);
		return JobUpsertResult.Updated;
	}

	public async Task LinkJobToCategoryAsync(int jobId, string categorySlug, CancellationToken ct = default)
	{
		var now = DateTimeOffset.UtcNow;
		LinkJobToCategory(jobId, categorySlug, now);
		await _db.SaveChangesAsync(ct);
	}

	public async Task<MetadataUpsertResult> UpsertMetadataAndLinkCategoryAsync(
		JobApplication job,
		string categorySlug,
		CancellationToken ct = default)
	{
		// Parallel category scrapes can race on the same jobs.ge id; retry once after PK conflict.
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				return await UpsertMetadataAndLinkCategoryCoreAsync(job, categorySlug, ct);
			}
			catch (DbUpdateException ex) when (attempt == 0 && IsUniqueViolation(ex))
			{
				_db.ChangeTracker.Clear();
			}
		}
	}

	private async Task<MetadataUpsertResult> UpsertMetadataAndLinkCategoryCoreAsync(
		JobApplication job,
		string categorySlug,
		CancellationToken ct)
	{
		var now = DateTimeOffset.UtcNow;
		var existing = await _db.Jobs.FindAsync([job.Id], ct);
		JobUpsertResult result;
		var needsDetailFetch = false;

		if (existing is null)
		{
			_db.Jobs.Add(MapToEntity(job, now, now, null, null));
			result = JobUpsertResult.Inserted;
			needsDetailFetch = true;
		}
		else if (HasSameMetadata(existing, job))
		{
			existing.LastSeenAt = now;
			result = JobUpsertResult.Skipped;
			needsDetailFetch = existing.DetailsFetchedAt is null;
		}
		else
		{
			ApplyMetadata(existing, job);
			existing.LastSeenAt = now;
			existing.UpdatedAt = now;
			existing.DetailsFetchedAt = null;
			result = JobUpsertResult.Updated;
			needsDetailFetch = true;
		}

		LinkJobToCategory(existing?.Id ?? job.Id, categorySlug, now);
		await _db.SaveChangesAsync(ct);
		return new MetadataUpsertResult(result, needsDetailFetch);
	}

	public async Task UpsertDescriptionAsync(
		int jobId,
		string description,
		string? descriptionHtml = null,
		CancellationToken ct = default)
	{
		var now = DateTimeOffset.UtcNow;
		var existing = await _db.Jobs.FindAsync([jobId], ct)
			?? throw new InvalidOperationException($"Job {jobId} not found.");

		var changed = existing.Description != description
			|| existing.DescriptionHtml != descriptionHtml;

		if (changed)
		{
			existing.Description = description;
			existing.DescriptionHtml = descriptionHtml;
			existing.UpdatedAt = now;
		}

		existing.DetailsFetchedAt = now;
		existing.LastSeenAt = now;
		ApplyEnrichment(existing, now);
		await _db.SaveChangesAsync(ct);
	}

	public async Task<EnrichmentBackfillResultDto> BackfillEnrichmentAsync(
		int limit = 100,
		CancellationToken ct = default)
	{
		limit = Math.Clamp(limit, 1, 500);
		var now = DateTimeOffset.UtcNow;

		var stale = await _db.Jobs
			.Where(j => j.Description != null && j.EnrichmentVersion < EnrichmentService.CurrentVersion)
			.OrderBy(j => j.Id)
			.Take(limit)
			.ToListAsync(ct);

		foreach (var job in stale)
			ApplyEnrichment(job, now);

		await _db.SaveChangesAsync(ct);

		var remaining = await _db.Jobs
			.CountAsync(j => j.Description != null && j.EnrichmentVersion < EnrichmentService.CurrentVersion, ct);

		return new EnrichmentBackfillResultDto(stale.Count, remaining);
	}

	public async Task<JobUpsertResult> UpsertAndLinkCategoryAsync(
		JobApplication job,
		string categorySlug,
		CancellationToken ct = default)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				return await UpsertAndLinkCategoryCoreAsync(job, categorySlug, ct);
			}
			catch (DbUpdateException ex) when (attempt == 0 && IsUniqueViolation(ex))
			{
				_db.ChangeTracker.Clear();
			}
		}
	}

	private async Task<JobUpsertResult> UpsertAndLinkCategoryCoreAsync(
		JobApplication job,
		string categorySlug,
		CancellationToken ct)
	{
		var now = DateTimeOffset.UtcNow;
		var existing = await _db.Jobs.FindAsync([job.Id], ct);
		JobUpsertResult result;

		if (existing is null)
		{
			_db.Jobs.Add(MapToEntity(job, now, now, null, job.Description is not null ? now : null));
			result = JobUpsertResult.Inserted;
		}
		else if (HasSameContent(existing, job))
		{
			existing.LastSeenAt = now;
			result = JobUpsertResult.Skipped;
		}
		else
		{
			ApplyMetadata(existing, job);
			existing.Description = job.Description;
			if (job.Description is not null)
				existing.DetailsFetchedAt = now;
			existing.LastSeenAt = now;
			existing.UpdatedAt = now;
			result = JobUpsertResult.Updated;
		}

		LinkJobToCategory(job.Id, categorySlug, now);
		await _db.SaveChangesAsync(ct);
		return result;
	}

	public async Task<JobsPageDto> GetJobsPageAsync(
		JobQuery query,
		int page,
		int pageSize,
		CancellationToken ct = default)
	{
		pageSize = Math.Clamp(pageSize, 1, _options.MaxJobsPageSize);
		page = Math.Max(page, 1);

		var filtered = ApplyJobFilters(query);

		var totalCount = await filtered.CountAsync(ct);

		var items = await filtered
			.OrderByDescending(j => j.LastSeenAt)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(j => new JobListItemDto(
				j.Id,
				j.Name,
				j.Link,
				j.Company,
				j.CompanyLink,
				j.Published,
				j.EndDate,
				j.LastSeenAt,
				j.SalaryMin,
				j.SalaryMax,
				j.SalaryCurrency,
				j.SalaryPeriod,
				j.City,
				j.WorkMode,
				j.EmploymentType,
				j.Seniority,
				j.LanguageRequirement))
			.ToListAsync(ct);

		var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

		return new JobsPageDto(items, totalCount, page, pageSize, totalPages);
	}

	public async Task<SearchPageDto> SearchJobsAsync(
		string q,
		string? categorySlug,
		int page,
		int pageSize,
		CancellationToken ct = default)
	{
		pageSize = Math.Clamp(pageSize, 1, _options.MaxJobsPageSize);
		page = Math.Max(page, 1);
		q = q.Trim();

		var fts = await SearchWithFtsAsync(q, categorySlug, page, pageSize, ct);
		if (fts.TotalCount > 0)
			return fts;

		return await SearchWithTrigramsAsync(q, categorySlug, page, pageSize, ct);
	}

	public async Task<JobDetailDto?> GetJobByIdAsync(int id, CancellationToken ct = default)
	{
		var entity = await _db.Jobs
			.AsNoTracking()
			.Include(j => j.JobCategories)
			.FirstOrDefaultAsync(j => j.Id == id, ct);

		if (entity is null)
			return null;

		return new JobDetailDto(
			entity.Id,
			entity.Name,
			entity.Link,
			entity.Company,
			entity.CompanyLink,
			entity.Published,
			entity.EndDate,
			entity.Description,
			entity.DetailsFetchedAt,
			entity.FirstSeenAt,
			entity.LastSeenAt,
			entity.UpdatedAt,
			entity.JobCategories.Select(jc => jc.CategorySlug).OrderBy(s => s).ToList(),
			entity.SalaryMin,
			entity.SalaryMax,
			entity.SalaryCurrency,
			entity.SalaryPeriod,
			entity.City,
			entity.WorkMode,
			entity.EmploymentType,
			entity.Seniority,
			entity.LanguageRequirement,
			entity.EnrichmentVersion,
			entity.EnrichedAt);
	}

	private async Task<SearchPageDto> SearchWithFtsAsync(
		string q,
		string? categorySlug,
		int page,
		int pageSize,
		CancellationToken ct)
	{
		// WebSearchToTsQuery must stay inside the expression tree; a local assignment
		// forces client evaluation and throws at runtime.
		var filtered = ApplyCategoryFilter(_db.Jobs.AsNoTracking(), categorySlug)
			.Where(j => j.SearchVector.Matches(EF.Functions.WebSearchToTsQuery("simple", q)));

		var totalCount = await filtered.CountAsync(ct);
		if (totalCount == 0)
			return new SearchPageDto([], 0, page, pageSize, 0, "fts");

		var items = await filtered
			.OrderByDescending(j => j.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("simple", q)))
			.ThenByDescending(j => j.LastSeenAt)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(j => new SearchResultDto(
				j.Id,
				j.Name,
				j.Link,
				j.Company,
				j.CompanyLink,
				j.Published,
				j.EndDate,
				j.LastSeenAt,
				(decimal)j.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("simple", q)),
				j.SalaryMin,
				j.SalaryMax,
				j.SalaryCurrency,
				j.SalaryPeriod,
				j.City,
				j.WorkMode,
				j.EmploymentType,
				j.Seniority,
				j.LanguageRequirement))
			.ToListAsync(ct);

		var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
		return new SearchPageDto(items, totalCount, page, pageSize, totalPages, "fts");
	}

	private async Task<SearchPageDto> SearchWithTrigramsAsync(
		string q,
		string? categorySlug,
		int page,
		int pageSize,
		CancellationToken ct)
	{
		var filtered = ApplyCategoryFilter(_db.Jobs.AsNoTracking(), categorySlug)
			.Where(j =>
				EF.Functions.TrigramsSimilarity(j.Name, q) >= TrigramSimilarityThreshold
				|| EF.Functions.TrigramsSimilarity(j.Company, q) >= TrigramSimilarityThreshold);

		var totalCount = await filtered.CountAsync(ct);
		var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

		var items = await filtered
			.OrderByDescending(j =>
				EF.Functions.TrigramsSimilarity(j.Name, q) >= EF.Functions.TrigramsSimilarity(j.Company, q)
					? EF.Functions.TrigramsSimilarity(j.Name, q)
					: EF.Functions.TrigramsSimilarity(j.Company, q))
			.ThenByDescending(j => j.LastSeenAt)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(j => new SearchResultDto(
				j.Id,
				j.Name,
				j.Link,
				j.Company,
				j.CompanyLink,
				j.Published,
				j.EndDate,
				j.LastSeenAt,
				(decimal)(EF.Functions.TrigramsSimilarity(j.Name, q) >= EF.Functions.TrigramsSimilarity(j.Company, q)
					? EF.Functions.TrigramsSimilarity(j.Name, q)
					: EF.Functions.TrigramsSimilarity(j.Company, q)),
				j.SalaryMin,
				j.SalaryMax,
				j.SalaryCurrency,
				j.SalaryPeriod,
				j.City,
				j.WorkMode,
				j.EmploymentType,
				j.Seniority,
				j.LanguageRequirement))
			.ToListAsync(ct);

		return new SearchPageDto(items, totalCount, page, pageSize, totalPages, "trgm");
	}

	private IQueryable<JobEntity> ApplyJobFilters(JobQuery query)
	{
		var jobs = ApplyCategoryFilter(_db.Jobs.AsNoTracking(), query.CategorySlug);

		if (!string.IsNullOrWhiteSpace(query.Search))
		{
			var pattern = $"%{query.Search}%";
			jobs = jobs.Where(j =>
				EF.Functions.ILike(j.Name, pattern) || EF.Functions.ILike(j.Company, pattern));
		}

		if (query.DotNetOnly)
			jobs = jobs.Where(j => EF.Functions.ILike(j.Name, "%.net%"));

		return jobs;
	}

	private static IQueryable<JobEntity> ApplyCategoryFilter(IQueryable<JobEntity> jobs, string? categorySlug)
	{
		if (string.IsNullOrWhiteSpace(categorySlug))
			return jobs;

		var slug = categorySlug;
		return jobs.Where(j => j.JobCategories.Any(jc => jc.CategorySlug == slug));
	}

	private void ApplyEnrichment(JobEntity entity, DateTimeOffset now)
	{
		var result = _enrichment.Extract(entity.Name, entity.Description, entity.DescriptionHtml);
		entity.SalaryMin = result.SalaryMin;
		entity.SalaryMax = result.SalaryMax;
		entity.SalaryCurrency = result.SalaryCurrency;
		entity.SalaryPeriod = result.SalaryPeriod;
		entity.City = result.City;
		entity.WorkMode = result.WorkMode;
		entity.EmploymentType = result.EmploymentType;
		entity.Seniority = result.Seniority;
		entity.LanguageRequirement = result.LanguageRequirement;
		entity.EnrichmentVersion = EnrichmentService.CurrentVersion;
		entity.EnrichedAt = now;
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
			Status = ScrapeRunStatus.Running,
			Phase = ScrapeRunPhase.Discovering,
			ProgressUpdatedAt = DateTimeOffset.UtcNow
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
		int detailsFetched,
		int detailsSkipped,
		string phase,
		int listingPagesFetched,
		int jobsDiscovered,
		int jobsNeedingDetails,
		CancellationToken ct = default)
	{
		var run = await _db.ScrapeRuns.FindAsync([runId], ct);
		if (run is null || run.Status != ScrapeRunStatus.Running)
			return;

		run.Inserted = inserted;
		run.Updated = updated;
		run.Skipped = skipped;
		run.Failed = failed;
		run.DetailsFetched = detailsFetched;
		run.DetailsSkipped = detailsSkipped;
		run.Phase = phase;
		run.ListingPagesFetched = listingPagesFetched;
		run.JobsDiscovered = jobsDiscovered;
		run.JobsNeedingDetails = jobsNeedingDetails;
		run.ProgressUpdatedAt = DateTimeOffset.UtcNow;
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
		run.DetailsFetched = result.DetailsFetched;
		run.DetailsSkipped = result.DetailsSkipped;
		run.Status = ScrapeRunStatus.Completed;
		run.Phase = ScrapeRunPhase.Completed;
		run.ProgressUpdatedAt = DateTimeOffset.UtcNow;
		await _db.SaveChangesAsync(ct);
	}

	public async Task FailScrapeRunAsync(long runId, string errorMessage, CancellationToken ct = default)
	{
		var run = await _db.ScrapeRuns.FindAsync([runId], ct);
		if (run is null)
			return;

		run.FinishedAt = DateTimeOffset.UtcNow;
		run.Status = ScrapeRunStatus.Failed;
		run.Phase = ScrapeRunPhase.Failed;
		run.ErrorMessage = errorMessage;
		run.ProgressUpdatedAt = DateTimeOffset.UtcNow;
		await _db.SaveChangesAsync(ct);
	}

	public async Task<int> AbandonRunningScrapeRunsAsync(string reason, CancellationToken ct = default)
	{
		var now = DateTimeOffset.UtcNow;
		return await _db.ScrapeRuns
			.Where(r => r.Status == ScrapeRunStatus.Running)
			.ExecuteUpdateAsync(
				s => s
					.SetProperty(r => r.Status, ScrapeRunStatus.Failed)
					.SetProperty(r => r.Phase, ScrapeRunPhase.Failed)
					.SetProperty(r => r.ErrorMessage, reason)
					.SetProperty(r => r.FinishedAt, now)
					.SetProperty(r => r.ProgressUpdatedAt, now),
				ct);
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

	private static bool HasSameMetadata(JobEntity existing, JobApplication job) =>
		existing.Name == job.Name
		&& existing.Link == job.Link
		&& existing.Company == job.Company
		&& existing.CompanyLink == job.CompanyLink
		&& existing.Published == job.Published
		&& existing.EndDate == job.EndDate;

	private static bool HasSameContent(JobEntity existing, JobApplication job) =>
		HasSameMetadata(existing, job)
		&& existing.Description == job.Description;

	private static void ApplyMetadata(JobEntity existing, JobApplication job)
	{
		existing.Name = job.Name;
		existing.Link = job.Link;
		existing.Company = job.Company;
		existing.CompanyLink = job.CompanyLink;
		existing.Published = job.Published;
		existing.EndDate = job.EndDate;
	}

	private void LinkJobToCategory(int jobId, string categorySlug, DateTimeOffset now)
	{
		var link = _db.JobCategories.Find([jobId, categorySlug]);
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
	}

	private static bool IsUniqueViolation(DbUpdateException ex) =>
		ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

	private static JobEntity MapToEntity(
		JobApplication job,
		DateTimeOffset firstSeenAt,
		DateTimeOffset lastSeenAt,
		DateTimeOffset? updatedAt,
		DateTimeOffset? detailsFetchedAt) =>
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
			DetailsFetchedAt = detailsFetchedAt,
			FirstSeenAt = firstSeenAt,
			LastSeenAt = lastSeenAt,
			UpdatedAt = updatedAt
		};
}
