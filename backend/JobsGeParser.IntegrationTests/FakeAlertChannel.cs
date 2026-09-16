using System.Collections.Concurrent;
using JobsGeParser.Alerts.Channels;
using JobsGeParser.Data;

namespace JobsGeParser.IntegrationTests;

/// <summary>
/// A scriptable channel. Records what it was asked to send and returns whatever outcome the test
/// queued, so retry, dead lettering and suppression can be exercised without a provider.
/// </summary>
public sealed class FakeAlertChannel(AlertChannelKind kind) : IAlertChannel
{
	private readonly ConcurrentQueue<AlertSendResult> _scripted = new();

	public AlertChannelKind Kind { get; } = kind;

	public ConcurrentBag<OutboundAlertMessage> Sent { get; } = [];

	public int SendCount => Sent.Count;

	/// <summary>
	/// Queues outcomes, consumed one per send. Once exhausted, sends succeed.
	/// </summary>
	public FakeAlertChannel Script(params AlertSendResult[] results)
	{
		foreach (var result in results)
			_scripted.Enqueue(result);

		return this;
	}

	public bool AlwaysFailTransiently { get; set; }

	public bool TryNormalizeAddress(string raw, out string normalized, out string? error)
	{
		normalized = (raw ?? "").Trim().ToLowerInvariant();
		error = normalized.Length == 0 ? "Address is required." : null;
		return error is null;
	}

	public Task<AlertSendResult> SendAsync(OutboundAlertMessage message, CancellationToken ct)
	{
		Sent.Add(message);

		if (_scripted.TryDequeue(out var scripted))
			return Task.FromResult(scripted);

		return Task.FromResult(AlwaysFailTransiently
			? AlertSendResult.Transient("scripted outage")
			: AlertSendResult.Sent($"fake-{message.MessageId}"));
	}
}
