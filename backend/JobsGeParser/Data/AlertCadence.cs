namespace JobsGeParser.Data;

public enum AlertCadence
{
	/// <summary>
	/// Enqueue a message as soon as the match is recorded.
	/// </summary>
	Instant = 0,

	/// <summary>
	/// Accumulate matches and send one batched message at the subscriber's local send time.
	/// </summary>
	DailyDigest = 1
}
