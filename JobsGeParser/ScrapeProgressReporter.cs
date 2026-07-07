namespace JobsGeParser;

public class ScrapeProgressReporter
{
	private readonly long _scrapeRunId;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly int _updateInterval;
	private readonly SemaphoreSlim _flushLock = new(1, 1);
	private int _completedTotal;

	public ScrapeProgressReporter(
		long scrapeRunId,
		IServiceScopeFactory scopeFactory,
		int updateInterval)
	{
		_scrapeRunId = scrapeRunId;
		_scopeFactory = scopeFactory;
		_updateInterval = Math.Max(1, updateInterval);
	}

	public async Task ReportAsync(int inserted, int updated, int skipped, int failed, CancellationToken ct)
	{
		var total = Interlocked.Increment(ref _completedTotal);
		if (total % _updateInterval != 0)
			return;

		await FlushAsync(inserted, updated, skipped, failed, ct);
	}

	public async Task FlushAsync(int inserted, int updated, int skipped, int failed, CancellationToken ct)
	{
		await _flushLock.WaitAsync(ct);
		try
		{
			using var scope = _scopeFactory.CreateScope();
			var repo = scope.ServiceProvider.GetRequiredService<Repo>();
			await repo.UpdateScrapeRunProgressAsync(_scrapeRunId, inserted, updated, skipped, failed, ct);
		}
		finally
		{
			_flushLock.Release();
		}
	}
}
