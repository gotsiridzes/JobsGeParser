using System.Collections.Frozen;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Alerts.Channels;

/// <summary>
/// Per-channel outbound gate, the same shape as <see cref="Scraping.ScrapeRequestThrottle"/>:
/// a concurrency semaphore plus a minimum interval between request starts. It also carries a
/// cooldown breaker so a provider outage cannot burn the retry budget of the whole queue.
/// </summary>
public sealed class AlertSendThrottle
{
	private readonly FrozenDictionary<AlertChannelKind, ChannelGate> _gates;
	private readonly int _failureThreshold;
	private readonly TimeSpan _cooldown;
	private readonly TimeProvider _timeProvider;

	public AlertSendThrottle(IOptions<AlertOptions> options, TimeProvider timeProvider)
	{
		var value = options.Value;
		_timeProvider = timeProvider;
		_failureThreshold = value.ChannelFailureThreshold;
		_cooldown = TimeSpan.FromSeconds(value.ChannelCooldownSeconds);

		_gates = new Dictionary<AlertChannelKind, ChannelGate>
		{
			[AlertChannelKind.Email] = new(value.Email.MaxConcurrency, value.Email.MinIntervalMs),
			[AlertChannelKind.Sms] = new(value.Sms.MaxConcurrency, value.Sms.MinIntervalMs)
		}.ToFrozenDictionary();
	}

	public async Task<T> ExecuteAsync<T>(AlertChannelKind kind, Func<Task<T>> action, CancellationToken ct)
	{
		var gate = _gates[kind];

		await gate.Concurrency.WaitAsync(ct);
		try
		{
			await gate.EnforceDelayAsync(_timeProvider, ct);
			return await action();
		}
		finally
		{
			gate.Concurrency.Release();
		}
	}

	/// <summary>
	/// True while a channel is cooling down after repeated transient failures. Callers must skip
	/// the channel entirely rather than attempting and burning an attempt.
	/// </summary>
	public bool IsCoolingDown(AlertChannelKind kind, out DateTimeOffset until)
	{
		var gate = _gates[kind];
		until = gate.CooldownUntil;
		return until > _timeProvider.GetUtcNow();
	}

	public void RecordSuccess(AlertChannelKind kind)
	{
		var gate = _gates[kind];
		lock (gate.StateLock)
		{
			gate.ConsecutiveFailures = 0;
			gate.CooldownUntil = DateTimeOffset.MinValue;
		}
	}

	/// <summary>
	/// Returns true when this failure opened a cooldown window.
	/// </summary>
	public bool RecordTransientFailure(AlertChannelKind kind)
	{
		var gate = _gates[kind];
		lock (gate.StateLock)
		{
			gate.ConsecutiveFailures++;
			if (gate.ConsecutiveFailures < _failureThreshold)
				return false;

			gate.ConsecutiveFailures = 0;
			gate.CooldownUntil = _timeProvider.GetUtcNow() + _cooldown;
			return true;
		}
	}

	private sealed class ChannelGate(int maxConcurrency, int minIntervalMs)
	{
		public SemaphoreSlim Concurrency { get; } = new(maxConcurrency, maxConcurrency);

		public object StateLock { get; } = new();

		public int ConsecutiveFailures { get; set; }

		public DateTimeOffset CooldownUntil { get; set; } = DateTimeOffset.MinValue;

		private readonly object _delayLock = new();
		private DateTimeOffset _nextSlotAt = DateTimeOffset.MinValue;

		/// <summary>
		/// Reserves the next slot under a lock before awaiting, so concurrent senders queue up
		/// instead of all measuring the same idle gap and firing together.
		/// </summary>
		public async Task EnforceDelayAsync(TimeProvider timeProvider, CancellationToken ct)
		{
			if (minIntervalMs <= 0)
				return;

			TimeSpan wait;
			lock (_delayLock)
			{
				var now = timeProvider.GetUtcNow();
				var slot = _nextSlotAt > now ? _nextSlotAt : now;
				wait = slot - now;
				_nextSlotAt = slot.AddMilliseconds(minIntervalMs);
			}

			if (wait > TimeSpan.Zero)
				await Task.Delay(wait, timeProvider, ct);
		}
	}
}
