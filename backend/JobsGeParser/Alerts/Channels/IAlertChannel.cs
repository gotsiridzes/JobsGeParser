using JobsGeParser.Data;

namespace JobsGeParser.Alerts.Channels;

/// <summary>
/// A delivery transport. Everything above this interface is channel agnostic, so adding Telegram,
/// a webhook or push means adding one class and a registration, with no change to matching.
/// </summary>
public interface IAlertChannel
{
	AlertChannelKind Kind { get; }

	/// <summary>
	/// What counts as a valid address is channel knowledge: email lowercasing and shape, or a
	/// phone number normalized to E.164.
	/// </summary>
	bool TryNormalizeAddress(string raw, out string normalized, out string? error);

	Task<AlertSendResult> SendAsync(OutboundAlertMessage message, CancellationToken ct);
}
