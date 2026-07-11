using System.Threading.Channels;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using JobsGeParser.Scraping;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Workers;

public class JobScrapeWorker(
	IOptions<JobsGeParserOptions> options,
	IServiceScopeFactory scopeFactory,
	ScrapeWorkerState workerState,
	ILogger<JobScrapeWorker> logger) : BackgroundService
{
	private readonly JobsGeParserOptions _options = options.Value;
	private readonly SemaphoreSlim _scrapeLock = new(1, 1);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.ScrapeEnabled)
		{
			logger.LogInformation("Scrape worker not started: scraping is disabled in configuration");
			return;
		}

		logger.LogInformation(
			"Scrape worker started: interval {IntervalMinutes} min, {CategoryCount} enabled categories, category concurrency {CategoryConcurrency}, detail concurrency {DetailConcurrency}",
			_options.ScrapeIntervalMinutes,
			_options.EnabledCategories.Count(),
			_options.CategoryScrapeConcurrency,
			_options.DetailFetchConcurrency);

		await AbandonOrphanedRunsAsync(stoppingToken);

		using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.ScrapeIntervalMinutes));

		try
		{
			if (_options.ScrapeOnStartup)
			{
				logger.LogInformation("Running scrape on startup");
				await RunScrapeAsync(stoppingToken);
			}

			while (await timer.WaitForNextTickAsync(stoppingToken))
				await RunScrapeAsync(stoppingToken);
		}
		finally
		{
			// Best-effort: graceful stop may leave runs Running if cancel raced with SaveChanges.
			await AbandonOrphanedRunsAsync(CancellationToken.None);
		}
	}

	private async Task AbandonOrphanedRunsAsync(CancellationToken ct)
	{
		using var scope = scopeFactory.CreateScope();
		var repo = scope.ServiceProvider.GetRequiredService<Repo>();
		var abandoned = await repo.AbandonRunningScrapeRunsAsync(
			"Abandoned: process stopped before scrape finished",
			ct);

		if (abandoned > 0)
		{
			logger.LogWarning(
				"Marked {Count} orphaned scrape run(s) as Failed (previous process stopped mid-batch)",
				abandoned);
		}
	}

	private async Task RunScrapeAsync(CancellationToken ct)
	{
		if (!await _scrapeLock.WaitAsync(0, ct))
		{
			workerState.RecordSkippedTick();
			logger.LogWarning("Skipping scrape tick because a previous scrape is still running.");
			return;
		}

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
		}
		finally
		{
			workerState.EndTick();
			_scrapeLock.Release();
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
