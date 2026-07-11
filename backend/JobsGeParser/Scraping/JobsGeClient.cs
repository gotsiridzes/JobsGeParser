using System.Diagnostics;
using System.Threading.Channels;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using JobsGeParser.Models;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Scraping;

public class JobsGeClient(
	IOptions<JobsGeParserOptions> options,
	IHttpClientFactory httpClientFactory,
	HtmlProcessor processor,
	IServiceScopeFactory scopeFactory,
	ScrapeRequestThrottle throttle,
	ILogger<JobsGeClient> logger)
{
	private readonly JobsGeParserOptions _ops = options.Value;
	private readonly HttpClient _client = httpClientFactory.CreateClient("JobsGeClient");

	public async Task<ScrapeResult> ScrapeCategoryAsync(
		long scrapeRunId,
		JobCategoryOptions category,
		CancellationToken ct = default)
	{
		var stopwatch = Stopwatch.StartNew();
		var counters = new ScrapeCounters();
		var progress = new ScrapeProgressReporter(scrapeRunId, scopeFactory, _ops.ProgressUpdateInterval);

		logger.LogInformation(
			"Fetching listing pages for {Category} (run {RunId})",
			category.Slug,
			scrapeRunId);

		var (jobs, listingPagesFetched) = await FetchAllListingJobsAsync(category, progress, ct);
		var jobsNeedingDetails = new List<JobApplication>(jobs.Count);

		if (jobs.Count == 0)
		{
			logger.LogWarning(
				"Listing for {Category} returned no parseable jobs (listUrl={ListUrl}). Page may be empty or HTML structure changed.",
				category.Slug,
				category.ListUrl);
		}

		await progress.FlushAsync(
			counters.ToSnapshot(ScrapeRunPhase.Discovering, listingPagesFetched, jobs.Count, 0),
			ct);

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
					logger.LogWarning(
						ex,
						"Metadata upsert failed for job {JobId} in {Category}",
						job.Id,
						category.Slug);
				}
			}
		}

		logger.LogInformation(
			"Category {Category}: {JobCount} unique jobs on listing - inserted={Inserted}, updated={Updated}, skipped={Skipped}, failed={Failed}, {NeedingDetails} need detail fetch",
			category.Slug,
			jobs.Count,
			counters.Inserted,
			counters.Updated,
			counters.Skipped,
			counters.Failed,
			jobsNeedingDetails.Count);

		await progress.FlushAsync(
			counters.ToSnapshot(
				ScrapeRunPhase.Enriching,
				listingPagesFetched,
				jobs.Count,
				jobsNeedingDetails.Count),
			ct);

		if (jobsNeedingDetails.Count > 0)
		{
			var concurrency = _ops.DetailFetchConcurrency;
			var channel = Channel.CreateBounded<JobApplication>(concurrency * 4);

			var consumers = Enumerable.Range(0, concurrency)
				.Select(_ => EnrichJobsAsync(
					channel.Reader,
					category.Slug,
					counters,
					progress,
					listingPagesFetched,
					jobs.Count,
					jobsNeedingDetails.Count,
					ct))
				.ToArray();

			foreach (var job in jobsNeedingDetails)
				await channel.Writer.WriteAsync(job, ct);

			channel.Writer.Complete();
			await Task.WhenAll(consumers);
		}

		stopwatch.Stop();

		await progress.FlushAsync(
			counters.ToSnapshot(
				ScrapeRunPhase.Enriching,
				listingPagesFetched,
				jobs.Count,
				jobsNeedingDetails.Count),
			ct);

		return new ScrapeResult(
			counters.Inserted,
			counters.Updated,
			counters.Skipped,
			counters.Failed,
			counters.DetailsFetched,
			counters.DetailsSkipped,
			stopwatch.Elapsed);
	}

	private async Task<(List<JobApplication> Jobs, int PagesFetched)> FetchAllListingJobsAsync(
		JobCategoryOptions category,
		ScrapeProgressReporter progress,
		CancellationToken ct)
	{
		var jobsById = new Dictionary<int, JobApplication>();
		var pagesFetched = 0;

		for (var page = 1; page <= _ops.MaxListingPages; page++)
		{
			var url = ListingUrlBuilder.ForPage(category.ListUrl, page);
			var response = await throttle.ExecuteAsync(
				() => _client.GetAsync(url, ct),
				ct);
			response.EnsureSuccessStatusCode();

			var content = await response.Content.ReadAsStringAsync(ct);
			var batch = processor.ParseHtmlAndGetJobApplicationsList(content).ToList();
			pagesFetched = page;

			if (batch.Count == 0)
			{
				logger.LogInformation(
					"Category {Category}: listing page {Page} empty - stopping pagination (unique={Unique})",
					category.Slug,
					page,
					jobsById.Count);
				await progress.FlushAsync(
					new ScrapeProgressSnapshot(
						0, 0, 0, 0, 0, 0,
						ScrapeRunPhase.Discovering,
						pagesFetched,
						jobsById.Count,
						0),
					ct);
				break;
			}

			var added = 0;
			foreach (var job in batch)
			{
				if (jobsById.TryAdd(job.Id, job))
					added++;
			}

			logger.LogInformation(
				"Category {Category}: listing page {Page} - parsed={Parsed}, new={New}, unique={Unique}",
				category.Slug,
				page,
				batch.Count,
				added,
				jobsById.Count);

			await progress.FlushAsync(
				new ScrapeProgressSnapshot(
					0, 0, 0, 0, 0, 0,
					ScrapeRunPhase.Discovering,
					pagesFetched,
					jobsById.Count,
					0),
				ct);

			// jobs.ge repeats the last scroll batch instead of returning empty HTML; stop when nothing new.
			if (added == 0)
			{
				logger.LogInformation(
					"Category {Category}: listing page {Page} added no new jobs - stopping pagination (unique={Unique})",
					category.Slug,
					page,
					jobsById.Count);
				break;
			}

			if (page == _ops.MaxListingPages)
			{
				logger.LogWarning(
					"Category {Category}: reached MaxListingPages ({MaxPages}); stopping pagination with {Unique} unique jobs",
					category.Slug,
					_ops.MaxListingPages,
					jobsById.Count);
			}
		}

		return (jobsById.Values.ToList(), pagesFetched);
	}

	private async Task EnrichJobsAsync(
		ChannelReader<JobApplication> reader,
		string categorySlug,
		ScrapeCounters counters,
		ScrapeProgressReporter progress,
		int listingPagesFetched,
		int jobsDiscovered,
		int jobsNeedingDetails,
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
				var parsed = processor.TryParseDescriptionDetail(detailContent);
				if (parsed is null)
				{
					counters.RecordFailed();
					logger.LogWarning(
						"Could not parse description for job {JobId} in {Category}",
						job.Id,
						categorySlug);
				}
				else
				{
					using var scope = scopeFactory.CreateScope();
					var repo = scope.ServiceProvider.GetRequiredService<Repo>();
					await repo.UpsertDescriptionAsync(job.Id, parsed.Text, parsed.Html, ct);
					counters.RecordDetailsFetched();
				}
			}
			catch (Exception ex)
			{
				counters.RecordFailed();
				logger.LogWarning(
					ex,
					"Detail fetch failed for job {JobId} in {Category}",
					job.Id,
					categorySlug);
			}

			await progress.ReportAsync(
				counters.ToSnapshot(
					ScrapeRunPhase.Enriching,
					listingPagesFetched,
					jobsDiscovered,
					jobsNeedingDetails),
				ct);
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

		public ScrapeProgressSnapshot ToSnapshot(
			string phase,
			int listingPagesFetched,
			int jobsDiscovered,
			int jobsNeedingDetails) =>
			new(
				Inserted,
				Updated,
				Skipped,
				Failed,
				DetailsFetched,
				DetailsSkipped,
				phase,
				listingPagesFetched,
				jobsDiscovered,
				jobsNeedingDetails);
	}
}
