using JobsGeParser.Configuration;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Alerts;

/// <summary>
/// Builds the short capability URLs. Short on purpose: these go into SMS bodies.
/// </summary>
public sealed class AlertLinkBuilder(IOptions<AlertOptions> options)
{
	private readonly string _baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');

	public string Manage(string token) => $"{_baseUrl}/a/{token}";

	public string Confirm(string token) => $"{_baseUrl}/a/c/{token}";

	public string Unsubscribe(string token) => $"{_baseUrl}/a/u/{token}";
}
