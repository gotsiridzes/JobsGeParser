using System.Threading.Channels;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using JobsGeParser.Workers;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Scraping;

/// <summary>
/// Runs one scrape tick: enabled categories via a bounded channel of parallel consumers.
/// Shared by <see cref="JobScrapeWorker"/> and performance tests.
/// </summary>
public class ScrapeBatchRunner(
	IOptions<JobsGeParserOptions> options,
	IServiceScopeFactory scopeFactory,
	ScrapeWorkerState workerState,
	ILogger<ScrapeBatchRunner> logger)
{
	private readonly JobsGeParserOptions _options = options.Value;

	public async Task<Guid> RunBatchAsync(CancellationToken ct = default)
	{
		var batchId = Guid.NewGuid();
		var categories = _options.EnabledCategories.ToList();

		logger.LogInformation(
			"Starting scrape batch {BatchId}: {CategoryCount} categories",
			batchId,
			categories.Count);

		workerState.BeginTick(batchId, categories.Select(c => c.Slug).ToList());

		try
		{
			var concurrency = _options.CategoryScrapeConcurrency;
			var channel = Channel.CreateBounded<JobCategoryOptions>(concurrency * 2);

			var consumers = Enumerable.Range(0, concurrency)
				.Select(_ => ConsumeCategoriesAsync(channel.Reader, batchId, ct))
				.ToArray();

			foreach (var category in categories)
				await channel.Writer.WriteAsync(category, ct);

			channel.Writer.Complete();
			await Task.WhenAll(consumers);

			logger.LogInformation("Scrape batch {BatchId} finished", batchId);
			return batchId;
		}
		finally
		{
			workerState.EndTick();
		}
	}

	private async Task ConsumeCategoriesAsync(
		ChannelReader<JobCategoryOptions> reader,
		Guid batchId,
		CancellationToken ct)
	{
		await foreach (var category in reader.ReadAllAsync(ct))
		{
			using var scope = scopeFactory.CreateScope();
			var client = scope.ServiceProvider.GetRequiredService<JobsGeClient>();
			var repo = scope.ServiceProvider.GetRequiredService<Repo>();

			var run = await repo.StartScrapeRunAsync(category.Slug, batchId, ct);
			workerState.BeginCategory(category.Slug, run.Id);

			logger.LogInformation(
				"Scrape run {RunId} started for category {Category} (batch {BatchId})",
				run.Id,
				category.Slug,
				batchId);

			try
			{
				var result = await client.ScrapeCategoryAsync(run.Id, category, ct);
				await repo.CompleteScrapeRunAsync(run.Id, result, ct);

				logger.LogInformation(
					"Scrape completed for {Category} in {Duration}: inserted={Inserted}, updated={Updated}, skipped={Skipped}, failed={Failed}, detailsFetched={DetailsFetched}, detailsSkipped={DetailsSkipped}",
					category.Slug,
					result.Duration,
					result.Inserted,
					result.Updated,
					result.Skipped,
					result.Failed,
					result.DetailsFetched,
					result.DetailsSkipped);
			}
			catch (Exception ex)
			{
				// Use None so a cancelled stopping token cannot block writing Failed status.
				var message = ex is OperationCanceledException
					? "Interrupted: application stopped before scrape finished"
					: ex.Message;
				await repo.FailScrapeRunAsync(run.Id, message, CancellationToken.None);

				if (ex is OperationCanceledException)
					logger.LogWarning("Scrape run interrupted for category {Category}.", category.Slug);
				else
					logger.LogError(ex, "Scrape run failed for category {Category}.", category.Slug);
			}
			finally
			{
				workerState.EndCategory(category.Slug, run.Id);
			}
		}
	}
}
