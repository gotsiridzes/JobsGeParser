using System.Collections.Frozen;
using JobsGeParser.Data;

namespace JobsGeParser.Alerts.Rendering;

public sealed class AlertRendererRegistry
{
	private readonly FrozenDictionary<AlertChannelKind, IAlertRenderer> _renderers;

	public AlertRendererRegistry(IEnumerable<IAlertRenderer> renderers)
	{
		var map = new Dictionary<AlertChannelKind, IAlertRenderer>();

		foreach (var renderer in renderers)
		{
			if (!map.TryAdd(renderer.Kind, renderer))
				throw new InvalidOperationException($"More than one alert renderer is registered for {renderer.Kind}.");
		}

		_renderers = map.ToFrozenDictionary();
	}

	public IAlertRenderer Get(AlertChannelKind kind) =>
		_renderers.TryGetValue(kind, out var renderer)
			? renderer
			: throw new InvalidOperationException($"No alert renderer is registered for {kind}.");
}
