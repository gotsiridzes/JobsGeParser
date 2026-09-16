namespace JobsGeParser.Data;

public static class AlertMessageKind
{
	public const string Confirmation = "Confirmation";

	public const string InstantMatch = "InstantMatch";

	public const string DailyDigest = "DailyDigest";

	/// <summary>
	/// The one-time batch of currently matching jobs sent right after confirmation, so a new
	/// subscriber sees the alert work immediately instead of waiting for the next new posting.
	/// </summary>
	public const string WelcomeBatch = "WelcomeBatch";

	public const string UnsubscribeAck = "UnsubscribeAck";
}
