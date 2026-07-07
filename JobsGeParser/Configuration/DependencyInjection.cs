using JobsGeParser.Data;
using JobsGeParser.Scraping;
using JobsGeParser.Workers;
using Microsoft.EntityFrameworkCore;

namespace JobsGeParser.Configuration;

public static class DependencyInjection
{
	public static IServiceCollection AddJobsGeService(
		this IServiceCollection self,
		JobsGeParserOptions options,
		string connectionString)
	{
		options.ValidateOptions();
		ValidateConnectionString(connectionString);

		self.AddSingleton(options);

		self.AddDbContext<JobsDbContext>(dbOptions =>
			dbOptions.UseNpgsql(connectionString));

		self.AddHttpClient("JobsGeClient", c =>
		{
			c.BaseAddress = new Uri(options.BaseUrl);
			c.Timeout = TimeSpan.FromSeconds(30);
		});

		self.AddSingleton<ScrapeWorkerState>();
		self.AddSingleton<ScrapeRequestThrottle>();

		self.AddScoped<JobsGeClient>()
			.AddSingleton<HtmlProcessor>()
			.AddScoped<Repo>();

		self.AddHostedService<JobScrapeWorker>();

		return self;
	}

	private static void ValidateOptions(this JobsGeParserOptions self)
	{
		if (self is null)
			throw new ArgumentNullException(nameof(self));

		if (self.BaseUrl is null)
			throw new ArgumentNullException(nameof(self.BaseUrl));

		if (self.Categories is null || self.Categories.Count == 0)
			throw new ArgumentException("At least one category is required.", nameof(self.Categories));

		if (!self.Categories.Any(c => c.Enabled))
			throw new ArgumentException("At least one enabled category is required.", nameof(self.Categories));

		var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var category in self.Categories)
		{
			if (string.IsNullOrWhiteSpace(category.Slug))
				throw new ArgumentException("Category slug is required.");

			if (!slugs.Add(category.Slug))
				throw new ArgumentException($"Duplicate category slug: {category.Slug}");

			if (string.IsNullOrWhiteSpace(category.Name))
				throw new ArgumentException($"Category name is required for slug: {category.Slug}");

			if (string.IsNullOrWhiteSpace(category.ListUrl))
				throw new ArgumentException($"Category list URL is required for slug: {category.Slug}");
		}

		if (self.ScrapeIntervalMinutes < 1)
			throw new ArgumentOutOfRangeException(nameof(self.ScrapeIntervalMinutes), "Must be at least 1 minute.");

		if (self.DetailPageDelayMs < 0)
			throw new ArgumentOutOfRangeException(nameof(self.DetailPageDelayMs), "Cannot be negative.");

		if (self.DetailFetchConcurrency < 1)
			throw new ArgumentOutOfRangeException(nameof(self.DetailFetchConcurrency), "Must be at least 1.");

		if (self.CategoryScrapeConcurrency < 1)
			throw new ArgumentOutOfRangeException(nameof(self.CategoryScrapeConcurrency), "Must be at least 1.");

		if (self.ProgressUpdateInterval < 1)
			throw new ArgumentOutOfRangeException(nameof(self.ProgressUpdateInterval), "Must be at least 1.");

		if (self.DefaultJobsPageSize < 1)
			throw new ArgumentOutOfRangeException(nameof(self.DefaultJobsPageSize), "Must be at least 1.");

		if (self.MaxJobsPageSize < 1)
			throw new ArgumentOutOfRangeException(nameof(self.MaxJobsPageSize), "Must be at least 1.");

		if (self.DefaultJobsPageSize > self.MaxJobsPageSize)
			throw new ArgumentOutOfRangeException(nameof(self.DefaultJobsPageSize), "Cannot exceed MaxJobsPageSize.");
	}

	private static void ValidateConnectionString(string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentException("Connection string 'JobsGeParser' is required.", nameof(connectionString));
	}
}
