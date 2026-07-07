using JobsGeParser.Data;

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
				ActiveCategoryRuns = [],
				CurrentCategorySlug = null,
				CurrentRunId = null
			};
		}
	}

	public void BeginCategory(string categorySlug, long runId)
	{
		lock (_lock)
		{
			var active = _snapshot.ActiveCategoryRuns.ToList();
			active.Add(new ActiveCategoryRunDto(categorySlug, runId));
			_snapshot = _snapshot with { ActiveCategoryRuns = active };
			SyncCurrentCategoryFields();
		}
	}

	public void EndCategory(string categorySlug, long runId)
	{
		lock (_lock)
		{
			var active = _snapshot.ActiveCategoryRuns
				.Where(r => r.CategorySlug != categorySlug || r.RunId != runId)
				.ToList();

			_snapshot = _snapshot with
			{
				ActiveCategoryRuns = active,
				CompletedCategoriesInCurrentTick = _snapshot.CompletedCategoriesInCurrentTick + 1
			};
			SyncCurrentCategoryFields();
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
				ActiveCategoryRuns = [],
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

	private void SyncCurrentCategoryFields()
	{
		if (_snapshot.ActiveCategoryRuns.Count == 1)
		{
			var only = _snapshot.ActiveCategoryRuns[0];
			_snapshot = _snapshot with
			{
				CurrentCategorySlug = only.CategorySlug,
				CurrentRunId = only.RunId
			};
		}
		else
		{
			_snapshot = _snapshot with
			{
				CurrentCategorySlug = null,
				CurrentRunId = null
			};
		}
	}
}

public record ActiveCategoryRunDto(string CategorySlug, long RunId);

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

	public IReadOnlyList<ActiveCategoryRunDto> ActiveCategoryRuns { get; init; } = [];

	public IReadOnlyList<string> CategoriesInCurrentTick { get; init; } = [];

	public int CompletedCategoriesInCurrentTick { get; init; }
}
