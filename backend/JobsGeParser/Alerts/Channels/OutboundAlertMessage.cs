namespace JobsGeParser.Alerts.Channels;

/// <summary>
/// Everything a channel needs to deliver one message. Bodies are already rendered, so sending is
/// pure I/O and a retry is byte identical to the first attempt.
/// </summary>
public sealed record OutboundAlertMessage(
	Guid MessageId,
	string Address,
	string? Subject,
	string BodyText,
	string? BodyHtml,
	string IdempotencyKey,
	string? UnsubscribeUrl);
