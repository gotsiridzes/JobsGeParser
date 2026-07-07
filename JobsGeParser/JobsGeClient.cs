using System.Diagnostics;

namespace JobsGeParser;

public class JobsGeClient
{
	private readonly HttpClient _client;
	private readonly JobsGeParserOptions _ops;
	private readonly HtmlProcessor _processor;
	private readonly Repo _repo;

	public JobsGeClient(
		JobsGeParserOptions ops,
		IHttpClientFactory httpClientFactory,
		HtmlProcessor processor,
		Repo repo)
	{
		_ops = ops;
		_processor = processor;
		_repo = repo;
		_client = httpClientFactory.CreateClient("JobsGeClient");
	}

	public async Task<ScrapeResult> ScrapeAsync(CancellationToken ct = default)
	{
		var stopwatch = Stopwatch.StartNew();
		var inserted = 0;
		var updated = 0;
		var skipped = 0;
		var failed = 0;

		var response = await _client.GetAsync(_ops.JobsListUrl, ct);
		response.EnsureSuccessStatusCode();

		var content = await response.Content.ReadAsStringAsync(ct);
		var jobs = _processor.ParseHtmlAndGetJobApplicationsList(content).ToList();

		foreach (var job in jobs)
		{
			ct.ThrowIfCancellationRequested();

			try
			{
				var detailResponse = await _client.GetAsync(job.Link, ct);
				detailResponse.EnsureSuccessStatusCode();

				var detailContent = await detailResponse.Content.ReadAsStringAsync(ct);
				job.SetDescription(_processor.ParseDescription(detailContent));

				var result = await _repo.UpsertAsync(job, ct);
				switch (result)
				{
					case JobUpsertResult.Inserted: inserted++; break;
					case JobUpsertResult.Updated: updated++; break;
					case JobUpsertResult.Skipped: skipped++; break;
				}
			}
			catch (Exception)
			{
				failed++;
			}

			if (_ops.DetailPageDelayMs > 0)
				await Task.Delay(_ops.DetailPageDelayMs, ct);
		}

		stopwatch.Stop();
		return new ScrapeResult(inserted, updated, skipped, failed, stopwatch.Elapsed);
	}
}
