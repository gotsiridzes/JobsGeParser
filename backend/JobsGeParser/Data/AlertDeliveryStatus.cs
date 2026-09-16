namespace JobsGeParser.Data;

public static class AlertDeliveryStatus
{
	/// <summary>
	/// Matched but not yet attached to a message. Pending rows are exactly the digest backlog.
	/// </summary>
	public const string Pending = "Pending";

	public const string Queued = "Queued";

	public const string Sent = "Sent";

	public const string Failed = "Failed";

	public const string Skipped = "Skipped";

	public const string Cancelled = "Cancelled";
}
