using JobsGeParser.Alerts;
using JobsGeParser.Alerts.Channels;
using JobsGeParser.Alerts.Rendering;
using JobsGeParser.Data;
using JobsGeParser.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Configuration;

public static class AlertServiceRegistration
{
	private const string ConsoleProvider = "Console";

	public static IServiceCollection AddAlertServices(
		this IServiceCollection self,
		IConfiguration configuration)
	{
		self.AddOptions<AlertOptions>()
			.BindConfiguration("Alerts")
			.ValidateOnStart();
		self.AddSingleton<IValidateOptions<AlertOptions>, AlertOptionsValidator>();

		self.TryAddTimeProvider();

		self.AddSingleton<AlertCriteriaValidator>();
		self.AddSingleton<AlertLinkBuilder>();
		self.AddSingleton<AlertSendThrottle>();
		self.AddSingleton<AlertSendPump>();
		self.AddSingleton<AlertOutboxDrainer>();
		self.AddSingleton<AlertDigestBuilder>();

		self.AddSingleton<IAlertRenderer, EmailAlertRenderer>();
		self.AddSingleton<IAlertRenderer, SmsAlertRenderer>();
		self.AddSingleton<AlertRendererRegistry>();

		self.AddAlertChannels(configuration);
		self.AddSingleton<AlertChannelRegistry>();

		self.AddScoped<AlertRepo>();
		self.AddScoped<AlertSubscriptionService>();

		self.AddAlertRateLimiting();

		// Read the same way the existing CORS registration reads its section, because options
		// are not yet resolvable while the container is still being built.
		if (configuration.GetValue("Alerts:Enabled", false))
		{
			self.AddHostedService<AlertOutboxWorker>();
			self.AddHostedService<AlertDigestWorker>();
			self.AddHostedService<AlertSendWorker>();
		}

		return self;
	}

	private static void AddAlertChannels(this IServiceCollection self, IConfiguration configuration)
	{
		var emailProvider = configuration["Alerts:Email:Provider"] ?? ConsoleProvider;
		var smsProvider = configuration["Alerts:Sms:Provider"] ?? ConsoleProvider;

		if (IsConsole(emailProvider))
		{
			self.AddConsoleChannel(AlertChannelKind.Email);
		}
		else
		{
			self.AddSingleton<IAlertChannel, PostmarkEmailChannel>();
			self.AddHttpClient(PostmarkEmailChannel.HttpClientName, (sp, client) =>
			{
				var email = sp.GetRequiredService<IOptions<AlertOptions>>().Value.Email;
				client.BaseAddress = new Uri(email.ApiBaseUrl);
				client.DefaultRequestHeaders.Add("X-Postmark-Server-Token", email.ApiKey);
				client.DefaultRequestHeaders.Add("Accept", "application/json");
			});
		}

		// SMS has no provider implementation yet, so anything other than Console would leave the
		// registry without a channel and fail at startup rather than silently dropping messages.
		if (!IsConsole(smsProvider))
			throw new InvalidOperationException($"Alerts.Sms.Provider '{smsProvider}' is not implemented; use 'Console'.");

		self.AddConsoleChannel(AlertChannelKind.Sms);
	}

	private static bool IsConsole(string provider) =>
		string.Equals(provider, ConsoleProvider, StringComparison.OrdinalIgnoreCase);

	private static void AddConsoleChannel(this IServiceCollection self, AlertChannelKind kind)
	{
		self.AddSingleton<IAlertChannel>(sp => new LoggingAlertChannel(
			kind,
			sp.GetRequiredService<IOptions<AlertOptions>>().Value,
			sp.GetRequiredService<ILogger<LoggingAlertChannel>>()));
	}

	private static void TryAddTimeProvider(this IServiceCollection self)
	{
		if (self.All(d => d.ServiceType != typeof(TimeProvider)))
			self.AddSingleton(TimeProvider.System);
	}
}
