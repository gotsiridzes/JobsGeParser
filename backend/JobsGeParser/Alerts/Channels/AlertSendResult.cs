namespace JobsGeParser.Alerts.Channels;

public enum AlertSendOutcome
{
	Sent = 0,

	/// <summary>
	/// Worth retrying: timeouts, 429, 5xx, connection failures.
	/// </summary>
	TransientFailure = 1,

	/// <summary>
	/// Retrying will not help: a bad address, a rejected payload, bad credentials.
	/// </summary>
	PermanentFailure = 2
}

/// <param name="SuppressSubscriber">
/// True only when the failure is about the address itself, such as an invalid or opted-out
/// recipient. A permanent failure that is our own fault dead letters the message without
/// punishing the subscriber.
/// </param>
public sealed record AlertSendResult(
	AlertSendOutcome Outcome,
	string? ProviderMessageId = null,
	string? Error = null,
	TimeSpan? RetryAfter = null,
	bool SuppressSubscriber = false)
{
	public static AlertSendResult Sent(string? providerMessageId = null) =>
		new(AlertSendOutcome.Sent, providerMessageId);

	public static AlertSendResult Transient(string error, TimeSpan? retryAfter = null) =>
		new(AlertSendOutcome.TransientFailure, Error: error, RetryAfter: retryAfter);

	public static AlertSendResult Permanent(string error, bool suppressSubscriber = false) =>
		new(AlertSendOutcome.PermanentFailure, Error: error, SuppressSubscriber: suppressSubscriber);
}
