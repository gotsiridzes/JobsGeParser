namespace JobsGeParser.Configuration;

public class AlertSmsOptions
{
	/// <summary>
	/// Console logs the rendered message instead of sending. Twilio sends for real.
	/// </summary>
	public string Provider { get; set; } = "Console";

	public string FromNumber { get; set; } = "";

	public string AccountSid { get; set; } = "";

	public string ApiKey { get; set; } = "";

	public string ApiBaseUrl { get; set; } = "https://api.twilio.com/";

	/// <summary>
	/// Prepended when a subscriber gives a local number without a country code.
	/// </summary>
	public string DefaultCountryCode { get; set; } = "+995";

	public int MaxConcurrency { get; set; } = 2;

	public int MinIntervalMs { get; set; } = 250;

	/// <summary>
	/// Hard cap on the rendered body. 320 is two GSM-7 segments.
	/// </summary>
	public int MaxLength { get; set; } = 320;

	public int MaxDigestItems { get; set; } = 3;

	/// <summary>
	/// False dead letters a message stuck in Claimed instead of retrying it, because a duplicate
	/// SMS costs money and annoys the recipient.
	/// </summary>
	public bool RetryAfterClaimTimeout { get; set; }
}
