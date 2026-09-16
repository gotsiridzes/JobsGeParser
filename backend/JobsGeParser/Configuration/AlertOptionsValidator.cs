using Microsoft.Extensions.Options;

namespace JobsGeParser.Configuration;

public sealed class AlertOptionsValidator : IValidateOptions<AlertOptions>
{
	private const string ConsoleProvider = "Console";

	public ValidateOptionsResult Validate(string? name, AlertOptions options)
	{
		if (!options.Enabled)
			return ValidateOptionsResult.Success;

		if (!Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out _))
			return ValidateOptionsResult.Fail("Alerts.PublicBaseUrl must be an absolute URL.");

		if (!Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out _))
			return ValidateOptionsResult.Fail("Alerts.ApiBaseUrl must be an absolute URL.");

		// A bad timezone must fail at startup, not at 09:00 on a Tuesday.
		if (!TimeZoneInfo.TryFindSystemTimeZoneById(options.DefaultTimeZoneId, out _))
			return ValidateOptionsResult.Fail($"Alerts.DefaultTimeZoneId is not a known timezone: {options.DefaultTimeZoneId}");

		if (options.TokenSecret.Length < 16)
		{
			return ValidateOptionsResult.Fail(
				"Alerts.TokenSecret must be at least 16 characters. Set it outside appsettings, for example: "
				+ "dotnet user-secrets set \"Alerts:TokenSecret\" \"<random value>\"");
		}

		if (options.EventSettleDelaySeconds < 0)
			return ValidateOptionsResult.Fail("Alerts.EventSettleDelaySeconds cannot be negative.");

		if (options.OutboxDrainIntervalSeconds < 1)
			return ValidateOptionsResult.Fail("Alerts.OutboxDrainIntervalSeconds must be at least 1.");

		if (options.OutboxBatchSize < 1)
			return ValidateOptionsResult.Fail("Alerts.OutboxBatchSize must be at least 1.");

		if (options.OutboxClaimTimeoutMinutes < 1)
			return ValidateOptionsResult.Fail("Alerts.OutboxClaimTimeoutMinutes must be at least 1.");

		if (options.MaxOutboxAttempts < 1)
			return ValidateOptionsResult.Fail("Alerts.MaxOutboxAttempts must be at least 1.");

		if (options.SendIntervalSeconds < 1)
			return ValidateOptionsResult.Fail("Alerts.SendIntervalSeconds must be at least 1.");

		if (options.SendBatchSize < 1)
			return ValidateOptionsResult.Fail("Alerts.SendBatchSize must be at least 1.");

		if (options.MaxSendAttempts < 1)
			return ValidateOptionsResult.Fail("Alerts.MaxSendAttempts must be at least 1.");

		if (options.RetryBaseDelaySeconds < 1)
			return ValidateOptionsResult.Fail("Alerts.RetryBaseDelaySeconds must be at least 1.");

		if (options.RetryMaxDelayMinutes * 60 < options.RetryBaseDelaySeconds)
			return ValidateOptionsResult.Fail("Alerts.RetryMaxDelayMinutes cannot be shorter than RetryBaseDelaySeconds.");

		if (options.DigestScanIntervalMinutes < 1)
			return ValidateOptionsResult.Fail("Alerts.DigestScanIntervalMinutes must be at least 1.");

		if (options.ConfirmationTokenTtlHours < 1)
			return ValidateOptionsResult.Fail("Alerts.ConfirmationTokenTtlHours must be at least 1.");

		if (options.MaxSubscriptionsPerSubscriber < 1)
			return ValidateOptionsResult.Fail("Alerts.MaxSubscriptionsPerSubscriber must be at least 1.");

		if (options.MaxConfirmationsPerAddressPerDay < 1)
			return ValidateOptionsResult.Fail("Alerts.MaxConfirmationsPerAddressPerDay must be at least 1.");

		if (options.MaxConfirmationsPerDay < 1)
			return ValidateOptionsResult.Fail("Alerts.MaxConfirmationsPerDay must be at least 1.");

		if (options.MaxConsecutiveFailures < 1)
			return ValidateOptionsResult.Fail("Alerts.MaxConsecutiveFailures must be at least 1.");

		if (options.MessageRetentionDays < 1)
			return ValidateOptionsResult.Fail("Alerts.MessageRetentionDays must be at least 1.");

		var email = ValidateEmail(options.Email);
		if (email.Failed)
			return email;

		return ValidateSms(options.Sms);
	}

	private static ValidateOptionsResult ValidateEmail(AlertEmailOptions email)
	{
		if (email.MaxConcurrency < 1)
			return ValidateOptionsResult.Fail("Alerts.Email.MaxConcurrency must be at least 1.");

		if (email.MinIntervalMs < 0)
			return ValidateOptionsResult.Fail("Alerts.Email.MinIntervalMs cannot be negative.");

		if (email.MaxDigestItems < 1)
			return ValidateOptionsResult.Fail("Alerts.Email.MaxDigestItems must be at least 1.");

		if (string.Equals(email.Provider, ConsoleProvider, StringComparison.OrdinalIgnoreCase))
			return ValidateOptionsResult.Success;

		if (string.IsNullOrWhiteSpace(email.ApiKey))
			return ValidateOptionsResult.Fail("Alerts.Email.ApiKey is required when the provider is not Console.");

		if (string.IsNullOrWhiteSpace(email.FromAddress))
			return ValidateOptionsResult.Fail("Alerts.Email.FromAddress is required when the provider is not Console.");

		if (!Uri.TryCreate(email.ApiBaseUrl, UriKind.Absolute, out _))
			return ValidateOptionsResult.Fail("Alerts.Email.ApiBaseUrl must be an absolute URL.");

		return ValidateOptionsResult.Success;
	}

	private static ValidateOptionsResult ValidateSms(AlertSmsOptions sms)
	{
		if (sms.MaxConcurrency < 1)
			return ValidateOptionsResult.Fail("Alerts.Sms.MaxConcurrency must be at least 1.");

		if (sms.MinIntervalMs < 0)
			return ValidateOptionsResult.Fail("Alerts.Sms.MinIntervalMs cannot be negative.");

		if (sms.MaxDigestItems < 1)
			return ValidateOptionsResult.Fail("Alerts.Sms.MaxDigestItems must be at least 1.");

		if (sms.MaxLength is < 70 or > 1600)
			return ValidateOptionsResult.Fail("Alerts.Sms.MaxLength must be between 70 and 1600.");

		if (!sms.DefaultCountryCode.StartsWith('+'))
			return ValidateOptionsResult.Fail("Alerts.Sms.DefaultCountryCode must start with '+'.");

		if (string.Equals(sms.Provider, ConsoleProvider, StringComparison.OrdinalIgnoreCase))
			return ValidateOptionsResult.Success;

		if (string.IsNullOrWhiteSpace(sms.ApiKey))
			return ValidateOptionsResult.Fail("Alerts.Sms.ApiKey is required when the provider is not Console.");

		if (string.IsNullOrWhiteSpace(sms.AccountSid))
			return ValidateOptionsResult.Fail("Alerts.Sms.AccountSid is required when the provider is not Console.");

		if (string.IsNullOrWhiteSpace(sms.FromNumber))
			return ValidateOptionsResult.Fail("Alerts.Sms.FromNumber is required when the provider is not Console.");

		if (!Uri.TryCreate(sms.ApiBaseUrl, UriKind.Absolute, out _))
			return ValidateOptionsResult.Fail("Alerts.Sms.ApiBaseUrl must be an absolute URL.");

		return ValidateOptionsResult.Success;
	}
}
