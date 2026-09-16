using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Alerts.Channels;

/// <summary>
/// Sends through Postmark's REST API using a named HttpClient, per the repo rule against static
/// HttpClient. Classifying failures correctly is the whole job here: a wrong call either loses an
/// alert or retries something that will never succeed.
/// </summary>
public sealed class PostmarkEmailChannel(
	IHttpClientFactory httpClientFactory,
	IOptions<AlertOptions> options,
	ILogger<PostmarkEmailChannel> logger) : IAlertChannel
{
	public const string HttpClientName = "AlertEmailClient";

	/// <summary>Postmark error codes that mean the address itself is the problem.</summary>
	private const int InactiveRecipient = 406;
	private const int InvalidEmail = 300;
	private const int HardBounce = 406;

	/// <summary>
	/// Postmark expects PascalCase property names. PostAsJsonAsync would otherwise apply the web
	/// defaults and send camelCase, which Postmark rejects.
	/// </summary>
	private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions = new()
	{
		PropertyNamingPolicy = null,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	private readonly AlertEmailOptions _options = options.Value.Email;

	public AlertChannelKind Kind => AlertChannelKind.Email;

	public bool TryNormalizeAddress(string raw, out string normalized, out string? error) =>
		EmailAddress.TryNormalize(raw, out normalized, out error);

	public async Task<AlertSendResult> SendAsync(OutboundAlertMessage message, CancellationToken ct)
	{
		var client = httpClientFactory.CreateClient(HttpClientName);

		var payload = new PostmarkRequest
		{
			From = string.IsNullOrWhiteSpace(_options.FromName)
				? _options.FromAddress
				: $"{_options.FromName} <{_options.FromAddress}>",
			To = message.Address,
			Subject = message.Subject ?? "",
			TextBody = message.BodyText,
			HtmlBody = message.BodyHtml,
			// Postmark dedupes on its side too, which turns a retry of an already delivered
			// message into a no-op rather than a second email.
			MessageStream = _options.MessageStream,
			Headers = BuildHeaders(message)
		};

		try
		{
			using var response = await client.PostAsJsonAsync("email", payload, SerializerOptions, ct);
			return await InterpretAsync(response, message, ct);
		}
		catch (TaskCanceledException) when (!ct.IsCancellationRequested)
		{
			return AlertSendResult.Transient("Postmark request timed out.");
		}
		catch (HttpRequestException ex)
		{
			return AlertSendResult.Transient($"Postmark request failed: {ex.Message}");
		}
	}

	private static PostmarkHeader[] BuildHeaders(OutboundAlertMessage message)
	{
		if (message.UnsubscribeUrl is null)
			return [];

		// RFC 8058: the One-Click pair lets a mail client unsubscribe without opening the page,
		// which keeps complaints down and protects sender reputation.
		return
		[
			new("List-Unsubscribe", $"<{message.UnsubscribeUrl}>"),
			new("List-Unsubscribe-Post", "List-Unsubscribe=One-Click")
		];
	}

	private async Task<AlertSendResult> InterpretAsync(
		HttpResponseMessage response,
		OutboundAlertMessage message,
		CancellationToken ct)
	{
		if (response.IsSuccessStatusCode)
		{
			var ok = await ReadAsync(response, ct);
			return AlertSendResult.Sent(ok?.MessageID);
		}

		if (IsTransient(response.StatusCode))
		{
			return AlertSendResult.Transient(
				$"Postmark returned {(int)response.StatusCode}.",
				response.Headers.RetryAfter?.Delta);
		}

		var body = await ReadAsync(response, ct);
		var error = body?.Message ?? $"Postmark returned {(int)response.StatusCode}.";

		// Only an address-related rejection should cost the subscriber their subscription. A bad
		// token or a malformed payload is our fault, so the message dies but the subscriber lives.
		var addressIsBad = body?.ErrorCode is InactiveRecipient or InvalidEmail or HardBounce
			|| response.StatusCode == HttpStatusCode.NotAcceptable;

		logger.LogWarning(
			"Postmark rejected message {MessageId}: {StatusCode} {ErrorCode} {Error}",
			message.MessageId,
			(int)response.StatusCode,
			body?.ErrorCode,
			error);

		return AlertSendResult.Permanent(error, suppressSubscriber: addressIsBad);
	}

	private static bool IsTransient(HttpStatusCode status) =>
		status is HttpStatusCode.RequestTimeout
			or HttpStatusCode.TooManyRequests
			or HttpStatusCode.InternalServerError
			or HttpStatusCode.BadGateway
			or HttpStatusCode.ServiceUnavailable
			or HttpStatusCode.GatewayTimeout;

	private static async Task<PostmarkResponse?> ReadAsync(HttpResponseMessage response, CancellationToken ct)
	{
		try
		{
			return await response.Content.ReadFromJsonAsync<PostmarkResponse>(SerializerOptions, ct);
		}
		catch
		{
			// A non-JSON body is not worth failing over; the status code already classified this.
			return null;
		}
	}

	private sealed class PostmarkRequest
	{
		public string From { get; init; } = "";

		public string To { get; init; } = "";

		public string Subject { get; init; } = "";

		public string TextBody { get; init; } = "";

		public string? HtmlBody { get; init; }

		public string MessageStream { get; init; } = "outbound";

		public PostmarkHeader[] Headers { get; init; } = [];
	}

	private sealed record PostmarkHeader(
		[property: JsonPropertyName("Name")] string Name,
		[property: JsonPropertyName("Value")] string Value);

	private sealed class PostmarkResponse
	{
		public string? MessageID { get; init; }

		public int? ErrorCode { get; init; }

		public string? Message { get; init; }
	}
}
