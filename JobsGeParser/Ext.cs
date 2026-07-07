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

		if (self.JobsListUrl is null)
			throw new ArgumentNullException(nameof(self.JobsListUrl));

		if (self.ScrapeIntervalMinutes < 1)
			throw new ArgumentOutOfRangeException(nameof(self.ScrapeIntervalMinutes), "Must be at least 1 minute.");

		if (self.DetailPageDelayMs < 0)
			throw new ArgumentOutOfRangeException(nameof(self.DetailPageDelayMs), "Cannot be negative.");
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
