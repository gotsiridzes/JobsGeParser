using System.Diagnostics;
using System.Threading.Channels;

namespace JobsGeParser;

public class JobsGeClient
{
	private readonly HttpClient _client;
	private readonly JobsGeParserOptions _ops;
	private readonly HtmlProcessor _processor;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ScrapeRequestThrottle _throttle;

	public JobsGeClient(
		JobsGeParserOptions ops,
		IHttpClientFactory httpClientFactory,
		HtmlProcessor processor,
		IServiceScopeFactory scopeFactory,
		ScrapeRequestThrottle throttle)
	{
		_ops = ops;
		_processor = processor;
		_scopeFactory = scopeFactory;
		_throttle = throttle;
		_client = httpClientFactory.CreateClient("JobsGeClient");
	}

	public async Task<ScrapeResult> ScrapeCategoryAsync(
		long scrapeRunId,
		JobCategoryOptions category,
		CancellationToken ct = default)
	{
		var stopwatch = Stopwatch.StartNew();
		var counters = new ScrapeCounters();

		var response = await _throttle.ExecuteAsync(
			() => _client.GetAsync(category.ListUrl, ct),
			ct);
		response.EnsureSuccessStatusCode();

		var content = await response.Content.ReadAsStringAsync(ct);
		var jobs = _processor.ParseHtmlAndGetJobApplicationsList(content).ToList();

		var concurrency = _ops.DetailFetchConcurrency;
		var channel = Channel.CreateBounded<JobApplication>(concurrency * 4);
		var progress = new ScrapeProgressReporter(scrapeRunId, _scopeFactory, _ops.ProgressUpdateInterval);

		var consumers = Enumerable.Range(0, concurrency)
			.Select(_ => ConsumeJobsAsync(channel.Reader, category, counters, progress, ct))
			.ToArray();

		foreach (var job in jobs)
			await channel.Writer.WriteAsync(job, ct);

		channel.Writer.Complete();

		await Task.WhenAll(consumers);

		stopwatch.Stop();

		await progress.FlushAsync(counters.Inserted, counters.Updated, counters.Skipped, counters.Failed, ct);

		return new ScrapeResult(
			counters.Inserted,
			counters.Updated,
			counters.Skipped,
			counters.Failed,
			stopwatch.Elapsed);
	}

	private async Task ConsumeJobsAsync(
		ChannelReader<JobApplication> reader,
		JobCategoryOptions category,
		ScrapeCounters counters,
		ScrapeProgressReporter progress,
		CancellationToken ct)
	{
		await foreach (var job in reader.ReadAllAsync(ct))
		{
			try
			{
				var detailResponse = await _throttle.ExecuteAsync(
					() => _client.GetAsync(job.Link, ct),
					ct);
				detailResponse.EnsureSuccessStatusCode();

				var detailContent = await detailResponse.Content.ReadAsStringAsync(ct);
				job.SetDescription(_processor.ParseDescription(detailContent));

				using var scope = _scopeFactory.CreateScope();
				var repo = scope.ServiceProvider.GetRequiredService<Repo>();
				var result = await repo.UpsertAndLinkCategoryAsync(job, category.Slug, ct);
				counters.Record(result);
			}
			catch (Exception)
			{
				counters.RecordFailed();
			}

			await progress.ReportAsync(counters.Inserted, counters.Updated, counters.Skipped, counters.Failed, ct);
		}
	}

	private sealed class ScrapeCounters
	{
		private int _inserted;
		private int _updated;
		private int _skipped;
		private int _failed;

		public int Inserted => _inserted;
		public int Updated => _updated;
		public int Skipped => _skipped;
		public int Failed => _failed;

		public void Record(JobUpsertResult result)
		{
			switch (result)
			{
				case JobUpsertResult.Inserted: Interlocked.Increment(ref _inserted); break;
				case JobUpsertResult.Updated: Interlocked.Increment(ref _updated); break;
				case JobUpsertResult.Skipped: Interlocked.Increment(ref _skipped); break;
			}
		}

		public void RecordFailed() => Interlocked.Increment(ref _failed);
	}
}
