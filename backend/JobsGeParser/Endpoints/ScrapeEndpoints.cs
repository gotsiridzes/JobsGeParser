using JobsGeParser.Configuration;
using JobsGeParser.Data;
using JobsGeParser.Endpoints.Dtos;
using JobsGeParser.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Endpoints;

public static class ScrapeEndpoints
{
	public static void RegisterScrapeEndpoints(this IEndpointRouteBuilder routeBuilder)
	{
		var scrape = routeBuilder.MapGroup("api/jobs/scrape/")
			.WithTags("Scrape");

		scrape.MapGet("status", async (Repo repo, CancellationToken ct) =>
			Results.Ok(await repo.GetLatestScrapeRunsPerCategoryAsync(ct)))
			.WithName("GetScrapeStatus")
			.WithSummary("Latest scrape run per enabled category");

		scrape.MapGet("status/{slug}", async (Repo repo, string slug, CancellationToken ct) =>
		{
			var run = await repo.GetLatestScrapeRunAsync(slug, ct);
			return run is null ? Results.NotFound() : Results.Ok(run);
		})
			.WithName("GetScrapeStatusByCategory")
			.WithSummary("Latest scrape run for one category");

		scrape.MapGet("worker", (IOptions<JobsGeParserOptions> options, ScrapeWorkerState workerState) =>
			Results.Ok(new ScrapeWorkerStatusDto(
				options.Value.ScrapeEnabled,
				options.Value.ScrapeIntervalMinutes,
				options.Value.ScrapeOnStartup,
				workerState.GetSnapshot())))
			.WithName("GetScrapeWorkerStatus")
			.WithSummary("Live background worker state");

		scrape.MapGet("overview", async (
			Repo repo,
			IOptions<JobsGeParserOptions> options,
			ScrapeWorkerState workerState,
			CancellationToken ct) =>
			Results.Ok(await repo.GetScrapeOverviewAsync(options.Value, workerState.GetSnapshot(), ct)))
			.WithName("GetScrapeOverview")
			.WithSummary("Full scrape picture: worker state, active runs, recent batches");

		scrape.MapGet("runs/active", async (Repo repo, CancellationToken ct) =>
			Results.Ok(await repo.GetActiveScrapeRunsAsync(ct)))
			.WithName("GetActiveScrapeRuns")
			.WithSummary("All runs with status Running");

		scrape.MapGet("runs", async (
			Repo repo,
			string? status,
			string? category,
			Guid? batchId,
			int? limit,
			int? offset,
			CancellationToken ct) =>
			Results.Ok(await repo.GetScrapeRunsAsync(
				status,
				category,
				batchId,
				limit ?? 50,
				offset ?? 0,
				ct)))
			.WithName("GetScrapeRuns")
			.WithSummary("Paginated scrape run history");

		scrape.MapGet("runs/{id:long}", async (Repo repo, long id, CancellationToken ct) =>
		{
			var run = await repo.GetScrapeRunByIdAsync(id, ct);
			return run is null ? Results.NotFound() : Results.Ok(run);
		})
			.WithName("GetScrapeRunById")
			.WithSummary("Single scrape run by id");

		scrape.MapGet("batches", async (Repo repo, int? limit, CancellationToken ct) =>
			Results.Ok(await repo.GetRecentBatchesAsync(limit ?? 20, ct)))
			.WithName("GetScrapeBatches")
			.WithSummary("Recent scrape ticks grouped by batch id");

		scrape.MapGet("batches/{batchId:guid}", async (Repo repo, Guid batchId, CancellationToken ct) =>
		{
			var page = await repo.GetScrapeRunsAsync(batchId: batchId, limit: 200, offset: 0, ct: ct);
			if (page.Items.Count == 0)
				return Results.NotFound();

			var runs = page.Items;
			return Results.Ok(new ScrapeBatchSummaryDto(
				batchId,
				runs.Min(r => r.StartedAt),
				runs.All(r => r.FinishedAt is not null) ? runs.Max(r => r.FinishedAt) : null,
				runs.Count,
				runs.Count(r => r.Status == ScrapeRunStatus.Running),
				runs.Count(r => r.Status == ScrapeRunStatus.Completed),
				runs.Count(r => r.Status == ScrapeRunStatus.Failed),
				runs));
		})
			.WithName("GetScrapeBatchById")
			.WithSummary("All category runs in one scrape tick");

		scrape.MapPost("enrichment/backfill", async (Repo repo, int? limit, CancellationToken ct) =>
			Results.Ok(await repo.BackfillEnrichmentAsync(limit ?? 100, ct)))
			.WithName("BackfillEnrichment")
			.WithSummary("Re-run structured field extraction for jobs with stale enrichment_version");
	}
}
