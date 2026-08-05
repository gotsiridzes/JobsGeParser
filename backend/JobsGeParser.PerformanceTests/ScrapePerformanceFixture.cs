using System.Collections.Concurrent;
using System.Diagnostics;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using JobsGeParser.Scraping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace JobsGeParser.PerformanceTests;

public sealed class ScrapePerformanceFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
		.WithImage("postgres:16-alpine")
		.WithDatabase("jobsgeparser_perf")
		.WithUsername("postgres")
		.WithPassword("postgres")
		.Build();

	public ConcurrentBag<ScrapePerfResult> Results { get; } = new();

	public string ConnectionString => _postgres.GetConnectionString();

	public async Task InitializeAsync()
	{
		await _postgres.StartAsync();

		await using var bootstrap = BuildServiceProvider(
			new ScrapePerfScenario(
				"bootstrap",
				DetailPageDelayMs: 500,
				DetailFetchConcurrency: 1,
				CategoryScrapeConcurrency: 1,
				HttpClientTimeoutSeconds: 30,
				MaxListingPages: 2,
				AllCategories: false));

		await using var scope = bootstrap.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
		await db.Database.MigrateAsync();

		var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<JobsGeParserOptions>>().Value;
		var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CategorySync");
		await CategorySync.SyncAsync(db, options, logger);
	}

	public async Task DisposeAsync()
	{
		if (!Results.IsEmpty)
		{
			var ordered = Results.OrderBy(r => r.ScenarioName).ToList();
			PerfReportWriter.WriteConsoleSummary(ordered);
			var reportDir = Path.Combine(AppContext.BaseDirectory, "TestResults");
			var path = PerfReportWriter.WriteMarkdown(ordered, reportDir);
			Console.WriteLine($"Performance report written to: {path}");
		}

		await _postgres.DisposeAsync();
	}

	public ServiceProvider BuildServiceProvider(ScrapePerfScenario scenario)
	{
		var categories = scenario.AllCategories
			? AllCategories
			: MatrixCategories;

		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:JobsGeParser"] = ConnectionString,
				["Database:AutoMigrate"] = "false",
				["Cors:AllowedOrigins:0"] = "http://localhost:5173",
				["JobsGeParserOptions:BaseUrl"] = "https://jobs.ge/",
				["JobsGeParserOptions:ScrapeEnabled"] = "false",
				["JobsGeParserOptions:ScrapeIntervalMinutes"] = "60",
				["JobsGeParserOptions:ScrapeOnStartup"] = "false",
				["JobsGeParserOptions:DetailPageDelayMs"] = scenario.DetailPageDelayMs.ToString(),
				["JobsGeParserOptions:DetailFetchConcurrency"] = scenario.DetailFetchConcurrency.ToString(),
				["JobsGeParserOptions:CategoryScrapeConcurrency"] = scenario.CategoryScrapeConcurrency.ToString(),
				["JobsGeParserOptions:HttpClientTimeoutSeconds"] = scenario.HttpClientTimeoutSeconds.ToString(),
				["JobsGeParserOptions:MaxListingPages"] = scenario.MaxListingPages.ToString(),
				["JobsGeParserOptions:ProgressUpdateInterval"] = "10",
				["JobsGeParserOptions:DefaultJobsPageSize"] = "20",
				["JobsGeParserOptions:MaxJobsPageSize"] = "100",
			})
			.Build();

		// Bind categories as a JSON section via additional configuration.
		var categoryConfig = new ConfigurationBuilder()
			.AddInMemoryCollection(BuildCategoryConfigEntries(categories))
			.Build();

		var merged = new ConfigurationBuilder()
			.AddConfiguration(config)
			.AddConfiguration(categoryConfig)
			.Build();

		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(merged);
		services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
		services.AddJobsGeService(merged);
		return services.BuildServiceProvider(validateScopes: true);
	}

	public async Task ResetScrapeDataAsync(ServiceProvider provider)
	{
		await using var scope = provider.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
		await db.Database.ExecuteSqlRawAsync(
			"""
			TRUNCATE TABLE job_categories, jobs, scrape_runs RESTART IDENTITY CASCADE;
			""");

		var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<JobsGeParserOptions>>().Value;
		var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CategorySync");
		await CategorySync.SyncAsync(db, options, logger);
	}

	public async Task<ScrapePerfResult> RunScenarioAsync(ScrapePerfScenario scenario)
	{
		await using var provider = BuildServiceProvider(scenario);
		await ResetScrapeDataAsync(provider);

		var runner = provider.GetRequiredService<ScrapeBatchRunner>();
		var stopwatch = Stopwatch.StartNew();
		var batchId = await runner.RunBatchAsync();
		stopwatch.Stop();

		await using var scope = provider.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
		var runs = await db.ScrapeRuns
			.AsNoTracking()
			.Where(r => r.BatchId == batchId)
			.ToListAsync();

		var result = new ScrapePerfResult(
			ScenarioName: scenario.Name,
			DetailPageDelayMs: scenario.DetailPageDelayMs,
			DetailFetchConcurrency: scenario.DetailFetchConcurrency,
			CategoryScrapeConcurrency: scenario.CategoryScrapeConcurrency,
			HttpClientTimeoutSeconds: scenario.HttpClientTimeoutSeconds,
			MaxListingPages: scenario.MaxListingPages,
			CategoryCount: runs.Count,
			Duration: stopwatch.Elapsed,
			Inserted: runs.Sum(r => r.Inserted),
			Updated: runs.Sum(r => r.Updated),
			Skipped: runs.Sum(r => r.Skipped),
			Failed: runs.Sum(r => r.Failed),
			DetailsFetched: runs.Sum(r => r.DetailsFetched),
			DetailsSkipped: runs.Sum(r => r.DetailsSkipped),
			JobsDiscovered: runs.Sum(r => r.JobsDiscovered),
			CompletedRuns: runs.Count(r => r.Status == ScrapeRunStatus.Completed),
			FailedRuns: runs.Count(r => r.Status == ScrapeRunStatus.Failed),
			ErrorSamples: runs
				.Where(r => !string.IsNullOrWhiteSpace(r.ErrorMessage))
				.Select(r => $"{r.CategorySlug}: {r.ErrorMessage}")
				.Take(5)
				.ToList());

		Results.Add(result);
		return result;
	}

	private static IEnumerable<KeyValuePair<string, string?>> BuildCategoryConfigEntries(
		IReadOnlyList<JobCategoryOptions> categories)
	{
		for (var i = 0; i < categories.Count; i++)
		{
			var c = categories[i];
			yield return new($"JobsGeParserOptions:Categories:{i}:Slug", c.Slug);
			yield return new($"JobsGeParserOptions:Categories:{i}:Name", c.Name);
			yield return new($"JobsGeParserOptions:Categories:{i}:ListUrl", c.ListUrl);
			yield return new($"JobsGeParserOptions:Categories:{i}:Enabled", c.Enabled ? "true" : "false");
		}
	}

	public static readonly IReadOnlyList<JobCategoryOptions> MatrixCategories =
	[
		new()
		{
			Slug = "it",
			Name = "IT/Programming",
			ListUrl = "?page=1&q=&cid=6&lid=0&jid=0",
			Enabled = true
		},
		new()
		{
			Slug = "finance-statistics",
			Name = "Finance, Statistics",
			ListUrl = "?page=1&q=&cid=3&lid=0&jid=0",
			Enabled = true
		},
		new()
		{
			Slug = "pr-marketing",
			Name = "PR/Marketing",
			ListUrl = "?page=1&q=&cid=4&lid=0&jid=0",
			Enabled = true
		},
	];

	public static readonly IReadOnlyList<JobCategoryOptions> AllCategories =
	[
		new() { Slug = "administration-management", Name = "Administration/Management", ListUrl = "?page=1&q=&cid=1&lid=0&jid=0", Enabled = true },
		new() { Slug = "finance-statistics", Name = "Finance, Statistics", ListUrl = "?page=1&q=&cid=3&lid=0&jid=0", Enabled = true },
		new() { Slug = "sales-procurement", Name = "Sales/Procurement", ListUrl = "?page=1&q=&cid=2&lid=0&jid=0", Enabled = true },
		new() { Slug = "pr-marketing", Name = "PR/Marketing", ListUrl = "?page=1&q=&cid=4&lid=0&jid=0", Enabled = true },
		new() { Slug = "general-technical-staff", Name = "General Technical Staff", ListUrl = "?page=1&q=&cid=18&lid=0&jid=0", Enabled = true },
		new() { Slug = "logistics-transport-distribution", Name = "Logistics/Transport/Distribution", ListUrl = "?page=1&q=&cid=5&lid=0&jid=0", Enabled = true },
		new() { Slug = "construction-repair", Name = "Construction/Repair", ListUrl = "?page=1&q=&cid=11&lid=0&jid=0", Enabled = true },
		new() { Slug = "cleaning", Name = "Cleaning", ListUrl = "?page=1&q=&cid=16&lid=0&jid=0", Enabled = true },
		new() { Slug = "security-safety", Name = "Security/Safety", ListUrl = "?page=1&q=&cid=17&lid=0&jid=0", Enabled = true },
		new() { Slug = "it", Name = "IT/Programming", ListUrl = "?page=1&q=&cid=6&lid=0&jid=0", Enabled = true },
		new() { Slug = "media-publishing", Name = "Media/Publishing", ListUrl = "?page=1&q=&cid=13&lid=0&jid=0", Enabled = true },
		new() { Slug = "education", Name = "Education", ListUrl = "?page=1&q=&cid=12&lid=0&jid=0", Enabled = true },
		new() { Slug = "law", Name = "Law", ListUrl = "?page=1&q=&cid=7&lid=0&jid=0", Enabled = true },
		new() { Slug = "medicine-pharmacy", Name = "Medicine/Pharmacy", ListUrl = "?page=1&q=&cid=8&lid=0&jid=0", Enabled = true },
		new() { Slug = "beauty-fashion", Name = "Beauty/Fashion", ListUrl = "?page=1&q=&cid=14&lid=0&jid=0", Enabled = true },
		new() { Slug = "food", Name = "Food", ListUrl = "?page=1&q=&cid=10&lid=0&jid=0", Enabled = true },
		new() { Slug = "other", Name = "Other", ListUrl = "?page=1&q=&cid=9&lid=0&jid=0", Enabled = true },
	];
}
