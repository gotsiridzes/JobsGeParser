using JobsGeParser.Configuration;
using JobsGeParser.Data;
using JobsGeParser.Scraping;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Workers;

public class JobScrapeWorker(
	IOptions<JobsGeParserOptions> options,
	IServiceScopeFactory scopeFactory,
	ScrapeBatchRunner batchRunner,
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

		try
		{
			await batchRunner.RunBatchAsync(ct);
		}
		finally
		{
			_scrapeLock.Release();
		}
	}
}
