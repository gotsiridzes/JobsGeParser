using JobsGeParser.Configuration;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Scraping;

public class ScrapeRequestThrottle
{
	private readonly JobsGeParserOptions _options;
	private readonly SemaphoreSlim _concurrency;
	private readonly object _delayLock = new();
	private DateTimeOffset _lastRequestAt = DateTimeOffset.MinValue;

	public ScrapeRequestThrottle(IOptions<JobsGeParserOptions> options)
	{
		_options = options.Value;
		_concurrency = new SemaphoreSlim(_options.DetailFetchConcurrency, _options.DetailFetchConcurrency);
	}

	public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
	{
		await _concurrency.WaitAsync(ct);
		try
		{
			await EnforceDelayAsync(ct);
			var result = await action();
			return result;
		}
		finally
		{
			_concurrency.Release();
		}
	}

	public Task ExecuteAsync(Func<Task> action, CancellationToken ct = default) =>
		ExecuteAsync(async () =>
		{
			await action();
			return true;
		}, ct);

	private async Task EnforceDelayAsync(CancellationToken ct)
	{
		if (_options.DetailPageDelayMs <= 0)
			return;

		int delayMs;
		lock (_delayLock)
		{
			var elapsed = DateTimeOffset.UtcNow - _lastRequestAt;
			delayMs = Math.Max(0, _options.DetailPageDelayMs - (int)elapsed.TotalMilliseconds);
			_lastRequestAt = DateTimeOffset.UtcNow.AddMilliseconds(delayMs);
		}

		if (delayMs > 0)
			await Task.Delay(delayMs, ct);
	}
}
