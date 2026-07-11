using JobsGeParser.Data;

namespace JobsGeParser.Scraping;

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

	public async Task ReportAsync(ScrapeProgressSnapshot snapshot, CancellationToken ct)
	{
		var total = Interlocked.Increment(ref _completedTotal);
		if (total % _updateInterval != 0)
			return;

		await FlushAsync(snapshot, ct);
	}

	public async Task FlushAsync(ScrapeProgressSnapshot snapshot, CancellationToken ct)
	{
		await _flushLock.WaitAsync(ct);
		try
		{
			using var scope = _scopeFactory.CreateScope();
			var repo = scope.ServiceProvider.GetRequiredService<Repo>();
			await repo.UpdateScrapeRunProgressAsync(
				_scrapeRunId,
				snapshot.Inserted,
				snapshot.Updated,
				snapshot.Skipped,
				snapshot.Failed,
				snapshot.DetailsFetched,
				snapshot.DetailsSkipped,
				snapshot.Phase,
				snapshot.ListingPagesFetched,
				snapshot.JobsDiscovered,
				snapshot.JobsNeedingDetails,
				ct);
		}
		finally
		{
			_flushLock.Release();
		}
	}
}

public record ScrapeProgressSnapshot(
	int Inserted,
	int Updated,
	int Skipped,
	int Failed,
	int DetailsFetched,
	int DetailsSkipped,
	string Phase,
	int ListingPagesFetched,
	int JobsDiscovered,
	int JobsNeedingDetails);
