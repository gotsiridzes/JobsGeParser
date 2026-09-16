namespace JobsGeParser.Configuration;

public static class AlertRateLimitPolicies
{
	/// <summary>
	/// Preview runs full text queries, so it is limited to keep it from becoming a cheap way to
	/// hammer the database.
	/// </summary>
	public const string Preview = "alerts-preview";

	/// <summary>
	/// Subscribe sends a message to an arbitrary caller-supplied address. Tightest limit here.
	/// </summary>
	public const string Subscribe = "alerts-subscribe";

	/// <summary>
	/// Token authenticated read and update calls.
	/// </summary>
	public const string Manage = "alerts-manage";
}
