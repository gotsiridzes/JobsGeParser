using JobsGeParser.Alerts;

namespace JobsGeParser.Tests;

public class AlertRetryScheduleTests
{
	private static readonly TimeSpan Base = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan Max = TimeSpan.FromMinutes(30);

	private static TimeSpan Delay(int attempts, TimeSpan? retryAfter = null) =>
		AlertRetrySchedule.NextDelay(attempts, Base, Max, retryAfter, jitterSample: 0);

	[Fact]
	public void FirstRetry_UsesTheBaseDelay()
	{
		Assert.Equal(Base, Delay(1));
	}

	[Fact]
	public void Delay_DoublesEachAttempt()
	{
		Assert.Equal(TimeSpan.FromSeconds(60), Delay(2));
		Assert.Equal(TimeSpan.FromSeconds(120), Delay(3));
		Assert.Equal(TimeSpan.FromSeconds(240), Delay(4));
	}

	[Fact]
	public void Delay_IsMonotonicAndCapped()
	{
		var previous = TimeSpan.Zero;

		for (var attempt = 1; attempt <= 40; attempt++)
		{
			var delay = Delay(attempt);
			Assert.True(delay >= previous, $"Attempt {attempt} went backwards.");
			Assert.True(delay <= Max, $"Attempt {attempt} exceeded the cap.");
			previous = delay;
		}
	}

	[Fact]
	public void Delay_DoesNotOverflowAtAbsurdAttemptCounts()
	{
		Assert.Equal(Max, Delay(int.MaxValue));
	}

	[Fact]
	public void Jitter_StaysWithinTwentyPercent()
	{
		var none = AlertRetrySchedule.NextDelay(3, Base, Max, jitterSample: 0);
		var full = AlertRetrySchedule.NextDelay(3, Base, Max, jitterSample: 1);

		Assert.Equal(none * 1.2, full);
	}

	[Fact]
	public void ProviderRetryAfter_WinsOnlyWhenItAsksForLonger()
	{
		Assert.Equal(TimeSpan.FromMinutes(5), Delay(1, TimeSpan.FromMinutes(5)));
		Assert.Equal(Base, Delay(1, TimeSpan.FromSeconds(1)));
	}

	[Fact]
	public void ZeroAttempts_IsTreatedAsTheFirstRetry()
	{
		Assert.Equal(Base, Delay(0));
	}
}
