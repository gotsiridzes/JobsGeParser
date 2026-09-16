using System.Net.Mail;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Alerts.Channels;

/// <summary>
/// Development channel. Logs the rendered message instead of sending it, so the whole double opt
/// in flow can be exercised locally with no provider account: subscribe, read the confirmation
/// URL out of the console, open it.
/// </summary>
public sealed class LoggingAlertChannel(
	AlertChannelKind kind,
	AlertOptions options,
	ILogger<LoggingAlertChannel> logger) : IAlertChannel
{
	public AlertChannelKind Kind { get; } = kind;

	public bool TryNormalizeAddress(string raw, out string normalized, out string? error) =>
		Kind == AlertChannelKind.Sms
			? PhoneAddress.TryNormalize(raw, options.Sms.DefaultCountryCode, out normalized, out error)
			: EmailAddress.TryNormalize(raw, out normalized, out error);

	public Task<AlertSendResult> SendAsync(OutboundAlertMessage message, CancellationToken ct)
	{
		logger.LogInformation(
			"""
			Alert message ({Channel}) to {Address}
			Subject: {Subject}
			{Body}
			""",
			Kind,
			message.Address,
			message.Subject ?? "(none)",
			message.BodyText);

		return Task.FromResult(AlertSendResult.Sent($"console-{message.MessageId}"));
	}
}

public static class EmailAddress
{
	public static bool TryNormalize(string raw, out string normalized, out string? error)
	{
		normalized = "";
		var trimmed = raw?.Trim() ?? "";

		if (trimmed.Length is 0 or > 320)
		{
			error = "Email address must be between 1 and 320 characters.";
			return false;
		}

		// MailAddress is the closest thing in the BCL to a real parser, and it rejects the
		// shapes that matter without hand rolling a doomed regex.
		if (!MailAddress.TryCreate(trimmed, out var parsed) || !parsed.Address.Contains('.'))
		{
			error = "Email address is not valid.";
			return false;
		}

		normalized = parsed.Address.ToLowerInvariant();
		error = null;
		return true;
	}
}

public static class PhoneAddress
{
	/// <summary>
	/// Normalizes to E.164. A local number gets the configured country code, which is what a
	/// Georgian subscriber typing their own number will supply.
	/// </summary>
	public static bool TryNormalize(string raw, string defaultCountryCode, out string normalized, out string? error)
	{
		normalized = "";
		var digits = new string((raw ?? "").Where(char.IsAsciiDigit).ToArray());
		var explicitCountryCode = (raw ?? "").TrimStart().StartsWith('+');

		if (digits.Length == 0)
		{
			error = "Phone number is required.";
			return false;
		}

		var candidate = explicitCountryCode ? $"+{digits}" : $"{defaultCountryCode}{digits}";
		var candidateDigits = candidate[1..];

		if (candidateDigits.Length is < 8 or > 15)
		{
			error = "Phone number must be between 8 and 15 digits including the country code.";
			return false;
		}

		normalized = candidate;
		error = null;
		return true;
	}
}
