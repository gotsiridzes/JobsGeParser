using JobsGeParser.Alerts.Channels;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace JobsGeParser.IntegrationTests;

/// <summary>
/// Real Postgres, real DI graph. Everything in these tests exists because it cannot be proven
/// without a database: SKIP LOCKED, ON CONFLICT dedup, the reverse match SQL, and full text
/// matching. The only substitution is the channel, so nothing is actually sent.
/// </summary>
public sealed class AlertsFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
		.WithImage("postgres:16-alpine")
		.WithDatabase("jobsgeparser_alerts")
		.WithUsername("postgres")
		.WithPassword("postgres")
		.Build();

	public const string TokenSecret = "integration-test-token-secret";

	public ServiceProvider Services { get; private set; } = null!;

	public FakeAlertChannel Email { get; } = new(AlertChannelKind.Email);

	public FakeAlertChannel Sms { get; } = new(AlertChannelKind.Sms);

	public FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-09-16T06:00:00Z"));

	public async Task InitializeAsync()
	{
		await _postgres.StartAsync();

		Services = BuildServiceProvider();

		await using var scope = Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
		await db.Database.MigrateAsync();

		var options = scope.ServiceProvider
			.GetRequiredService<Microsoft.Extensions.Options.IOptions<JobsGeParserOptions>>().Value;
		var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CategorySync");
		await JobsGeParser.Scraping.CategorySync.SyncAsync(db, options, logger);
	}

	public async Task DisposeAsync()
	{
		await Services.DisposeAsync();
		await _postgres.DisposeAsync();
	}

	private ServiceProvider BuildServiceProvider()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:JobsGeParser"] = _postgres.GetConnectionString(),
				["Database:AutoMigrate"] = "false",
				["JobsGeParserOptions:BaseUrl"] = "https://jobs.ge/",
				["JobsGeParserOptions:ScrapeEnabled"] = "false",
				["JobsGeParserOptions:Categories:0:Slug"] = "it",
				["JobsGeParserOptions:Categories:0:Name"] = "IT/Programming",
				["JobsGeParserOptions:Categories:0:ListUrl"] = "?page=1&q=&cid=6&lid=0&jid=0",
				["JobsGeParserOptions:Categories:0:Enabled"] = "true",
				["JobsGeParserOptions:Categories:1:Slug"] = "finance-statistics",
				["JobsGeParserOptions:Categories:1:Name"] = "Finance, Statistics",
				["JobsGeParserOptions:Categories:1:ListUrl"] = "?page=1&q=&cid=3&lid=0&jid=0",
				["JobsGeParserOptions:Categories:1:Enabled"] = "true",
				["Alerts:Enabled"] = "true",
				["Alerts:TokenSecret"] = TokenSecret,
				["Alerts:PublicBaseUrl"] = "https://example.test",
				["Alerts:ApiBaseUrl"] = "https://api.example.test",
				// Events are claimable immediately; the settle delay is a production nicety that
				// would otherwise make every test wait a minute.
				["Alerts:EventSettleDelaySeconds"] = "0",
				["Alerts:RetryBaseDelaySeconds"] = "1",
				["Alerts:MaxSendAttempts"] = "3"
			})
			.Build();

		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(config);
		services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
		services.AddSingleton<TimeProvider>(Clock);
		services.AddJobsGeService(config);

		// Replace the console channels with fakes, so nothing leaves the process and outcomes
		// can be scripted.
		services.RemoveAll<IAlertChannel>();
		services.AddSingleton<IAlertChannel>(Email);
		services.AddSingleton<IAlertChannel>(Sms);

		return services.BuildServiceProvider(validateScopes: true);
	}

	/// <summary>
	/// Clears every alert and job row between tests. Truncating jobs cascades into job_events and
	/// alert_deliveries, which is exactly what the foreign keys are there for.
	/// </summary>
	public async Task ResetAsync()
	{
		await using var scope = Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();

		await db.Database.ExecuteSqlRawAsync("""
			TRUNCATE TABLE alert_deliveries, alert_messages, alert_subscriptions, alert_subscribers,
				job_events, job_categories, jobs RESTART IDENTITY CASCADE;
			""");

		Email.Sent.Clear();
		Sms.Sent.Clear();
		Email.AlwaysFailTransiently = false;
		Sms.AlwaysFailTransiently = false;
	}

	public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

	public T Resolve<T>() where T : notnull => Services.GetRequiredService<T>();
}

/// <summary>
/// Minimal controllable clock. The digest logic is timezone and calendar sensitive, so tests must
/// be able to move time rather than wait for it.
/// </summary>
public sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
	private DateTimeOffset _now = start;

	public override DateTimeOffset GetUtcNow() => _now;

	public void SetUtcNow(DateTimeOffset value) => _now = value;

	public void Advance(TimeSpan by) => _now = _now.Add(by);
}
