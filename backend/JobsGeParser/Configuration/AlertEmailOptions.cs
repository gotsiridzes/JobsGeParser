namespace JobsGeParser.Configuration;

public class AlertEmailOptions
{
	/// <summary>
	/// Console logs the rendered message instead of sending, so the full opt in flow works
	/// with no provider account. Postmark sends for real.
	/// </summary>
	public string Provider { get; set; } = "Console";

	public string FromAddress { get; set; } = "";

	public string FromName { get; set; } = "JobsGe Alerts";

	public string ApiBaseUrl { get; set; } = "https://api.postmarkapp.com/";

	public string ApiKey { get; set; } = "";

	public int MaxConcurrency { get; set; } = 4;

	public int MinIntervalMs { get; set; } = 100;

	public int MaxDigestItems { get; set; } = 25;

	/// <summary>
	/// True returns a message stuck in Claimed back to Pending after the claim timeout.
	/// A duplicate email is cheaper than a lost alert, so email opts in.
	/// </summary>
	public bool RetryAfterClaimTimeout { get; set; } = true;
}
