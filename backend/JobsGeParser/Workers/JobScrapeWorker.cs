using System.Threading.Channels;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using JobsGeParser.Scraping;

namespace JobsGeParser.Workers;

public class JobScrapeWorker(
	JobsGeParserOptions options,
	IServiceScopeFactory scopeFactory,
	ScrapeWorkerState workerState,
	ILogger<JobScrapeWorker> logger) : BackgroundService
{
	private readonly SemaphoreSlim _scrapeLock = new(1, 1);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!options.ScrapeEnabled)
		{
			logger.LogInformation("Job scraping is disabled.");
			return;
		}

		using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.ScrapeIntervalMinutes));

		if (options.ScrapeOnStartup)
			await RunScrapeAsync(stoppingToken);

		while (await timer.WaitForNextTickAsync(stoppingToken))
			await RunScrapeAsync(stoppingToken);
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
		var categories = options.EnabledCategories.ToList();

		workerState.BeginTick(batchId, categories.Select(c => c.Slug).ToList());

		try
		{
			var concurrency = options.CategoryScrapeConcurrency;
			var channel = Channel.CreateBounded<JobCategoryOptions>(concurrency * 2);

			var consumers = Enumerable.Range(0, concurrency)
				.Select(_ => ConsumeCategoriesAsync(channel.Reader, batchId, ct))
				.ToArray();

			foreach (var category in categories)
				await channel.Writer.WriteAsync(category, ct);

			channel.Writer.Complete();

			await Task.WhenAll(consumers);
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
				await repo.FailScrapeRunAsync(run.Id, ex.Message, ct);
				logger.LogError(ex, "Scrape run failed for category {Category}.", category.Slug);
			}
			finally
			{
				workerState.EndCategory(category.Slug, run.Id);
			}
		}
	}
}
