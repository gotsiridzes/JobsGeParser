namespace JobsGeParser.Data;

/// <summary>
/// A verified contact. Identity is the (Channel, Address) pair, so wanting both email and SMS
/// means two subscribers, each proven independently. No accounts, no passwords.
/// </summary>
public class AlertSubscriberEntity
{
	public Guid Id { get; set; }

	public AlertChannelKind Channel { get; set; }

	/// <summary>
	/// Normalized by the channel: lowercased trimmed email, or E.164 phone number.
	/// </summary>
	public string Address { get; set; } = null!;

	public string Status { get; set; } = null!;

	/// <summary>
	/// IANA timezone id used to decide when a daily digest is due.
	/// </summary>
	public string TimeZoneId { get; set; } = null!;

	public string? Locale { get; set; }

	/// <summary>
	/// SHA-256 of the capability token that manages this subscriber. The raw token is never stored.
	/// </summary>
	public byte[] ManageTokenHash { get; set; } = null!;

	public byte[]? ConfirmationTokenHash { get; set; }

	public DateTimeOffset? ConfirmationSentAt { get; set; }

	public DateTimeOffset? ConfirmationExpiresAt { get; set; }

	public int ConfirmationSendCount { get; set; }

	public DateTimeOffset? ConfirmedAt { get; set; }

	public DateTimeOffset? UnsubscribedAt { get; set; }

	public int ConsecutiveFailures { get; set; }

	public DateTimeOffset? SuppressedAt { get; set; }

	public string? SuppressionReason { get; set; }

	public DateTimeOffset CreatedAt { get; set; }

	public DateTimeOffset UpdatedAt { get; set; }

	public ICollection<AlertSubscriptionEntity> Subscriptions { get; set; } = [];
}
