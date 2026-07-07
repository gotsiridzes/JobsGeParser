using JobsGeParser.Configuration;
using JobsGeParser.Data;
using JobsGeParser.Endpoints.Dtos;
using JobsGeParser.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JobsGeParser.Endpoints;

public static class ScrapeEndpoints
{
	public static void RegisterScrapeEndpoints(this IEndpointRouteBuilder routeBuilder)
	{
		var scrape = routeBuilder.MapGroup("api/jobs/scrape/");

		scrape.MapGet("status", async (Repo repo, CancellationToken ct) =>
			Results.Ok(await repo.GetLatestScrapeRunsPerCategoryAsync(ct)));

		scrape.MapGet("status/{slug}", async (Repo repo, string slug, CancellationToken ct) =>
		{
			var run = await repo.GetLatestScrapeRunAsync(slug, ct);
			return run is null ? Results.NotFound() : Results.Ok(run);
		});

		scrape.MapGet("worker", (JobsGeParserOptions options, ScrapeWorkerState workerState) =>
			Results.Ok(new ScrapeWorkerStatusDto(
				options.ScrapeEnabled,
				options.ScrapeIntervalMinutes,
				options.ScrapeOnStartup,
				workerState.GetSnapshot())));

		scrape.MapGet("overview", async (
			Repo repo,
			JobsGeParserOptions options,
			ScrapeWorkerState workerState,
			CancellationToken ct) =>
			Results.Ok(await repo.GetScrapeOverviewAsync(options, workerState.GetSnapshot(), ct)));

		scrape.MapGet("runs/active", async (Repo repo, CancellationToken ct) =>
			Results.Ok(await repo.GetActiveScrapeRunsAsync(ct)));

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
				ct)));

		scrape.MapGet("runs/{id:long}", async (Repo repo, long id, CancellationToken ct) =>
		{
			var run = await repo.GetScrapeRunByIdAsync(id, ct);
			return run is null ? Results.NotFound() : Results.Ok(run);
		});

		scrape.MapGet("batches", async (Repo repo, int? limit, CancellationToken ct) =>
			Results.Ok(await repo.GetRecentBatchesAsync(limit ?? 20, ct)));

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
		});
	}
}
