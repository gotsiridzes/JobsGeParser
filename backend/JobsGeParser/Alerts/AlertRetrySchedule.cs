namespace JobsGeParser.Alerts;

/// <summary>
/// Exponential backoff with jitter. Pure, so the schedule is unit tested rather than observed.
/// </summary>
public static class AlertRetrySchedule
{
	private const double JitterFraction = 0.2;

	/// <summary>
	/// Delay before the next attempt. <paramref name="attempts"/> is the number already made,
	/// so the first retry uses the base delay.
	/// </summary>
	public static TimeSpan NextDelay(
		int attempts,
		TimeSpan baseDelay,
		TimeSpan maxDelay,
		TimeSpan? providerRetryAfter = null,
		double jitterSample = 0.5)
	{
		var exponent = Math.Max(attempts - 1, 0);

		// Cap the exponent before shifting so a stuck message cannot overflow the multiplier.
		var multiplier = exponent >= 16 ? 65536d : Math.Pow(2, exponent);

		var scaled = TimeSpan.FromTicks((long)Math.Min(baseDelay.Ticks * multiplier, maxDelay.Ticks));
		var jitter = TimeSpan.FromTicks((long)(scaled.Ticks * JitterFraction * Math.Clamp(jitterSample, 0, 1)));
		var delay = scaled + jitter;

		// A provider that told us how long to wait knows better, but only when it wants longer.
		if (providerRetryAfter is { } retryAfter && retryAfter > delay)
			return retryAfter;

		return delay;
	}

	public static TimeSpan NextDelayWithRandomJitter(
		int attempts,
		TimeSpan baseDelay,
		TimeSpan maxDelay,
		TimeSpan? providerRetryAfter = null) =>
		NextDelay(attempts, baseDelay, maxDelay, providerRetryAfter, Random.Shared.NextDouble());
}
