using System.Diagnostics;
using System.Threading.Channels;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using JobsGeParser.Models;

namespace JobsGeParser.Scraping;

public class JobsGeClient(
	JobsGeParserOptions ops,
	IHttpClientFactory httpClientFactory,
	HtmlProcessor processor,
	IServiceScopeFactory scopeFactory,
	ScrapeRequestThrottle throttle,
	ILogger<JobsGeClient> logger)
{
	private readonly HttpClient _client = httpClientFactory.CreateClient("JobsGeClient");

	public async Task<ScrapeResult> ScrapeCategoryAsync(
		long scrapeRunId,
		JobCategoryOptions category,
		CancellationToken ct = default)
	{
		var stopwatch = Stopwatch.StartNew();
		var counters = new ScrapeCounters();

		var response = await throttle.ExecuteAsync(
			() => _client.GetAsync(category.ListUrl, ct),
			ct);
		response.EnsureSuccessStatusCode();

		var content = await response.Content.ReadAsStringAsync(ct);
		var jobs = processor.ParseHtmlAndGetJobApplicationsList(content).ToList();
		var jobsNeedingDetails = new List<JobApplication>(jobs.Count);

		if (jobs.Count == 0)
		{
			logger.LogWarning(
				"Listing for {Category} returned no parseable jobs (url={ListUrl}). Page may be empty or HTML structure changed.",
				category.Slug,
				category.ListUrl);
		}

		using (var discoverScope = scopeFactory.CreateScope())
		{
			var repo = discoverScope.ServiceProvider.GetRequiredService<Repo>();
			foreach (var job in jobs)
			{
				try
				{
					var metadataResult = await repo.UpsertMetadataAndLinkCategoryAsync(job, category.Slug, ct);
					counters.RecordMetadata(metadataResult.Result);
					if (metadataResult.NeedsDetailFetch)
						jobsNeedingDetails.Add(job);
					else
						counters.RecordDetailsSkipped();
				}
				catch (Exception ex)
				{
					counters.RecordFailed();
					logger.LogDebug(ex, "Metadata upsert failed for job {JobId} in {Category}.", job.Id, category.Slug);
				}
			}
		}

		if (jobsNeedingDetails.Count > 0)
		{
			var concurrency = ops.DetailFetchConcurrency;
			var channel = Channel.CreateBounded<JobApplication>(concurrency * 4);
			var progress = new ScrapeProgressReporter(scrapeRunId, scopeFactory, ops.ProgressUpdateInterval);

			var consumers = Enumerable.Range(0, concurrency)
				.Select(_ => EnrichJobsAsync(channel.Reader, category.Slug, counters, progress, ct))
				.ToArray();

			foreach (var job in jobsNeedingDetails)
				await channel.Writer.WriteAsync(job, ct);

			channel.Writer.Complete();
			await Task.WhenAll(consumers);
		}

		stopwatch.Stop();

		using (var flushScope = scopeFactory.CreateScope())
		{
			var repo = flushScope.ServiceProvider.GetRequiredService<Repo>();
			await repo.UpdateScrapeRunProgressAsync(
				scrapeRunId,
				counters.Inserted,
				counters.Updated,
				counters.Skipped,
				counters.Failed,
				counters.DetailsFetched,
				counters.DetailsSkipped,
				ct);
		}

		return new ScrapeResult(
			counters.Inserted,
			counters.Updated,
			counters.Skipped,
			counters.Failed,
			counters.DetailsFetched,
			counters.DetailsSkipped,
			stopwatch.Elapsed);
	}

	private async Task EnrichJobsAsync(
		ChannelReader<JobApplication> reader,
		string categorySlug,
		ScrapeCounters counters,
		ScrapeProgressReporter progress,
		CancellationToken ct)
	{
		await foreach (var job in reader.ReadAllAsync(ct))
		{
			try
			{
				var detailResponse = await throttle.ExecuteAsync(
					() => _client.GetAsync(job.Link, ct),
					ct);
				detailResponse.EnsureSuccessStatusCode();

				var detailContent = await detailResponse.Content.ReadAsStringAsync(ct);
				var description = processor.TryParseDescription(detailContent);
				if (description is null)
				{
					counters.RecordFailed();
					logger.LogDebug("Could not parse description for job {JobId} in {Category}.", job.Id, categorySlug);
				}
				else
				{
					using var scope = scopeFactory.CreateScope();
					var repo = scope.ServiceProvider.GetRequiredService<Repo>();
					await repo.UpsertDescriptionAsync(job.Id, description, ct);
					counters.RecordDetailsFetched();
				}
			}
			catch (Exception ex)
			{
				counters.RecordFailed();
				logger.LogDebug(ex, "Detail fetch failed for job {JobId} in {Category}.", job.Id, categorySlug);
			}

			await progress.ReportAsync(counters.ToSnapshot(), ct);
		}
	}

	private sealed class ScrapeCounters
	{
		private int _inserted;
		private int _updated;
		private int _skipped;
		private int _failed;
		private int _detailsFetched;
		private int _detailsSkipped;

		public int Inserted => _inserted;
		public int Updated => _updated;
		public int Skipped => _skipped;
		public int Failed => _failed;
		public int DetailsFetched => _detailsFetched;
		public int DetailsSkipped => _detailsSkipped;

		public void RecordMetadata(JobUpsertResult result)
		{
			switch (result)
			{
				case JobUpsertResult.Inserted: Interlocked.Increment(ref _inserted); break;
				case JobUpsertResult.Updated: Interlocked.Increment(ref _updated); break;
				case JobUpsertResult.Skipped: Interlocked.Increment(ref _skipped); break;
			}
		}

		public void RecordDetailsFetched() => Interlocked.Increment(ref _detailsFetched);
		public void RecordDetailsSkipped() => Interlocked.Increment(ref _detailsSkipped);
		public void RecordFailed() => Interlocked.Increment(ref _failed);

		public ScrapeProgressSnapshot ToSnapshot() =>
			new(Inserted, Updated, Skipped, Failed, DetailsFetched, DetailsSkipped);
	}
}
