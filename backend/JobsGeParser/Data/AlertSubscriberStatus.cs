namespace JobsGeParser.Data;

public static class AlertSubscriberStatus
{
	/// <summary>
	/// Address supplied but not yet proven. Nothing but confirmation messages may be sent.
	/// </summary>
	public const string Pending = "Pending";

	public const string Confirmed = "Confirmed";

	public const string Unsubscribed = "Unsubscribed";

	/// <summary>
	/// Delivery failed permanently or the provider reported the address as bad. No further sends.
	/// </summary>
	public const string Suppressed = "Suppressed";
}
