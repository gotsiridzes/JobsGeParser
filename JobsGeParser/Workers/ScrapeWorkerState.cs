namespace JobsGeParser.Workers;

public class ScrapeWorkerState
{
	private readonly object _lock = new();

	public ScrapeWorkerSnapshot GetSnapshot()
	{
		lock (_lock)
			return _snapshot with { };
	}

	private ScrapeWorkerSnapshot _snapshot = new();

	public void BeginTick(Guid batchId, IReadOnlyList<string> categorySlugs)
	{
		lock (_lock)
		{
			_snapshot = _snapshot with
			{
				IsTickInProgress = true,
				CurrentBatchId = batchId,
				CurrentTickStartedAt = DateTimeOffset.UtcNow,
				LastTickStartedAt = DateTimeOffset.UtcNow,
				CategoriesInCurrentTick = categorySlugs,
				CompletedCategoriesInCurrentTick = 0,
				CurrentCategorySlug = null,
				CurrentRunId = null
			};
		}
	}

	public void BeginCategory(string categorySlug, long runId)
	{
		lock (_lock)
		{
			_snapshot = _snapshot with
			{
				CurrentCategorySlug = categorySlug,
				CurrentRunId = runId
			};
		}
	}

	public void EndCategory()
	{
		lock (_lock)
		{
			_snapshot = _snapshot with
			{
				CompletedCategoriesInCurrentTick = _snapshot.CompletedCategoriesInCurrentTick + 1,
				CurrentCategorySlug = null,
				CurrentRunId = null
			};
		}
	}

	public void EndTick()
	{
		lock (_lock)
		{
			_snapshot = _snapshot with
			{
				IsTickInProgress = false,
				LastTickCompletedAt = DateTimeOffset.UtcNow,
				CurrentBatchId = null,
				CurrentTickStartedAt = null,
				CategoriesInCurrentTick = [],
				CompletedCategoriesInCurrentTick = 0,
				CurrentCategorySlug = null,
				CurrentRunId = null
			};
		}
	}

	public void RecordSkippedTick()
	{
		lock (_lock)
		{
			_snapshot = _snapshot with
			{
				SkippedTicks = _snapshot.SkippedTicks + 1,
				LastSkippedTickAt = DateTimeOffset.UtcNow
			};
		}
	}
}

public record ScrapeWorkerSnapshot
{
	public bool IsTickInProgress { get; init; }

	public Guid? CurrentBatchId { get; init; }

	public DateTimeOffset? CurrentTickStartedAt { get; init; }

	public DateTimeOffset? LastTickStartedAt { get; init; }

	public DateTimeOffset? LastTickCompletedAt { get; init; }

	public DateTimeOffset? LastSkippedTickAt { get; init; }

	public int SkippedTicks { get; init; }

	public string? CurrentCategorySlug { get; init; }

	public long? CurrentRunId { get; init; }

	public IReadOnlyList<string> CategoriesInCurrentTick { get; init; } = [];

	public int CompletedCategoriesInCurrentTick { get; init; }
}
