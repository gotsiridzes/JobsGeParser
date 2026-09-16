namespace JobsGeParser.Data;

public static class AlertMessageStatus
{
	public const string Pending = "Pending";

	/// <summary>
	/// Leased by a send pump. Attempts is already incremented, so a crash cannot retry forever.
	/// </summary>
	public const string Claimed = "Claimed";

	public const string Sent = "Sent";

	public const string Failed = "Failed";

	public const string DeadLettered = "DeadLettered";

	public const string Cancelled = "Cancelled";
}
