namespace JobsGeParser.Data;

/// <summary>
/// The dedup ledger, and at the same time the digest accumulation buffer. The unique index on
/// (SubscriptionId, JobId) is the entire no-duplicate-alerts guarantee: it is enforced by Postgres,
/// so no crash, race, replay or double drain can produce a second alert for the same job.
/// </summary>
public class AlertDeliveryEntity
{
	public long Id { get; set; }

	public Guid SubscriptionId { get; set; }

	public AlertSubscriptionEntity? Subscription { get; set; }

	public int JobId { get; set; }

	public JobEntity? Job { get; set; }

	public DateTimeOffset MatchedAt { get; set; }

	public string Status { get; set; } = null!;

	public Guid? MessageId { get; set; }

	public AlertMessageEntity? Message { get; set; }

	public string? SkipReason { get; set; }
}
