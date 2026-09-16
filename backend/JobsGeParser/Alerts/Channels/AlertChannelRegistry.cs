using System.Collections.Frozen;
using JobsGeParser.Data;

namespace JobsGeParser.Alerts.Channels;

/// <summary>
/// Resolves a channel by kind. Preferred over keyed DI because the constructor catches a missing
/// or duplicated registration once at startup instead of at the first send, and because a test
/// can build this directly over fakes without touching the container.
/// </summary>
public sealed class AlertChannelRegistry
{
	private readonly FrozenDictionary<AlertChannelKind, IAlertChannel> _channels;

	public AlertChannelRegistry(IEnumerable<IAlertChannel> channels)
	{
		var map = new Dictionary<AlertChannelKind, IAlertChannel>();

		foreach (var channel in channels)
		{
			if (!map.TryAdd(channel.Kind, channel))
				throw new InvalidOperationException($"More than one alert channel is registered for {channel.Kind}.");
		}

		_channels = map.ToFrozenDictionary();
	}

	public bool Supports(AlertChannelKind kind) => _channels.ContainsKey(kind);

	public IAlertChannel Get(AlertChannelKind kind) =>
		_channels.TryGetValue(kind, out var channel)
			? channel
			: throw new InvalidOperationException($"No alert channel is registered for {kind}.");
}
