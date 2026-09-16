namespace JobsGeParser.Data;

/// <summary>
/// Transactional outbox row. Written in the same SaveChanges as the job upsert that produced it,
/// then drained by the alert outbox worker. Never written by the enrichment backfill.
/// </summary>
public class JobEventEntity
{
	public long Id { get; set; }

	public int JobId { get; set; }

	public JobEntity? Job { get; set; }

	public string EventType { get; set; } = null!;

	/// <summary>
	/// Set only for JobCategoryLinked events. Informational, no foreign key.
	/// </summary>
	public string? CategorySlug { get; set; }

	public DateTimeOffset OccurredAt { get; set; }

	/// <summary>
	/// Earliest time this event may be claimed. Lets a scrape tick settle before matching runs.
	/// </summary>
	public DateTimeOffset AvailableAt { get; set; }

	public DateTimeOffset? ClaimedAt { get; set; }

	public DateTimeOffset? ProcessedAt { get; set; }

	public int Attempts { get; set; }

	public string? LastError { get; set; }
}
