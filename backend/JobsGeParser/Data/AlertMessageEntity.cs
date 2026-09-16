namespace JobsGeParser.Data;

/// <summary>
/// One outbound message. Bodies are rendered at enqueue and persisted, so sending is pure I/O,
/// every retry is byte identical, and a post mortem can read exactly what was sent.
/// </summary>
public class AlertMessageEntity
{
	public Guid Id { get; set; }

	public Guid SubscriberId { get; set; }

	public AlertSubscriberEntity? Subscriber { get; set; }

	/// <summary>
	/// Null for confirmation messages, which belong to the subscriber rather than a filter.
	/// </summary>
	public Guid? SubscriptionId { get; set; }

	public AlertSubscriptionEntity? Subscription { get; set; }

	public string Kind { get; set; } = null!;

	/// <summary>
	/// Denormalized at enqueue so a later subscriber edit cannot retarget an in flight message.
	/// </summary>
	public AlertChannelKind Channel { get; set; }

	public string Address { get; set; } = null!;

	public string IdempotencyKey { get; set; } = null!;

	public string Status { get; set; } = null!;

	public string? Subject { get; set; }

	public string BodyText { get; set; } = null!;

	public string? BodyHtml { get; set; }

	public DateTimeOffset AvailableAt { get; set; }

	public DateTimeOffset? ClaimedAt { get; set; }

	public DateTimeOffset? SentAt { get; set; }

	public int Attempts { get; set; }

	public string? LastError { get; set; }

	public string? ProviderMessageId { get; set; }

	public DateTimeOffset CreatedAt { get; set; }

	public ICollection<AlertDeliveryEntity> Deliveries { get; set; } = [];
}
