namespace JobsGeParser.Data;

public static class AlertDeliverySkipReason
{
	public const string PreExisting = "PreExisting";

	public const string DigestOverflow = "DigestOverflow";

	public const string Unsubscribed = "Unsubscribed";

	/// <summary>
	/// The same person already received, or has queued, this job through another alert.
	/// </summary>
	public const string AlreadySent = "AlreadySent";
}
