namespace JobsGeParser.Data;

public static class AlertSubscriptionStatus
{
	/// <summary>
	/// Created but the owning subscriber has not proven their address yet.
	/// </summary>
	public const string PendingConfirmation = "PendingConfirmation";

	public const string Active = "Active";

	public const string Paused = "Paused";

	public const string Deleted = "Deleted";
}
