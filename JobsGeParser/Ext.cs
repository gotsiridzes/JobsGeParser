using JobsGeParser.Data;
using JobsGeParser.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobsGeParser;

public static class Ext
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

		self.AddHttpClient("JobsGeClient", c => c.BaseAddress = new Uri(options.BaseUrl));

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

		if (self.ProgressUpdateInterval < 1)
			throw new ArgumentOutOfRangeException(nameof(self.ProgressUpdateInterval), "Must be at least 1.");
	}

	private static void ValidateConnectionString(string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentException("Connection string 'JobsGeParser' is required.", nameof(connectionString));
	}

	public static DateOnly GetDate(this string value)
	{
		int year = DateTime.Now.Year;
		string[] split = value.Split(' ');
		int day = int.Parse(split[0]);
		int month = GetMonth(split[1]);

		return new(year, month, day);
	}

	private static int GetMonth(string value) =>
		value switch
		{
			"იანვარი" => 1,
			"თებერვალი" => 2,
			"მარტი" => 3,
			"აპრილი" => 4,
			"მაისი" => 5,
			"ივნისი" => 6,
			"ივლისი" => 7,
			"აგვისტო" => 8,
			"სექტემბერი" => 9,
			"ოქტომბერი" => 10,
			"ნოემბერი" => 11,
			"დეკემბერი" => 12,
			_ => throw new Exception("Invalid month")
		};
}
