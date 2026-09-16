using JobsGeParser.Configuration;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Alerts;

/// <summary>
/// Builds the short capability URLs. Short on purpose: these go into SMS bodies.
///
/// Human links point at <see cref="AlertOptions.PublicBaseUrl"/>, where the frontend serves the
/// confirm, manage and unsubscribe pages. The one-click unsubscribe link points at the API
/// instead, because a mail client POSTs to it directly with no JavaScript to forward the call.
/// </summary>
public sealed class AlertLinkBuilder(IOptions<AlertOptions> options)
{
	private readonly string _publicBaseUrl = options.Value.PublicBaseUrl.TrimEnd('/');
	private readonly string _apiBaseUrl = options.Value.ApiBaseUrl.TrimEnd('/');

	public string Manage(string token) => $"{_publicBaseUrl}/a/{token}";

	public string Confirm(string token) => $"{_publicBaseUrl}/a/c/{token}";

	public string Unsubscribe(string token) => $"{_publicBaseUrl}/a/u/{token}";

	/// <summary>
	/// RFC 8058 target for the List-Unsubscribe header.
	/// </summary>
	public string OneClickUnsubscribe(string token) => $"{_apiBaseUrl}/a/u/{token}";
}
