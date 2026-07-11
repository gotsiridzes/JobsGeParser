using JobsGeParser.Data;
using JobsGeParser.Enrichment;
using JobsGeParser.Scraping;
using JobsGeParser.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Configuration;

public static class DependencyInjection
{
	public static IServiceCollection AddJobsGeService(
		this IServiceCollection self,
		IConfiguration configuration)
	{
		var connectionString = configuration.GetConnectionString("JobsGeParser")
			?? throw new InvalidOperationException("Connection string 'JobsGeParser' is not configured.");

		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentException("Connection string 'JobsGeParser' is required.");

		self.AddOptions<JobsGeParserOptions>()
			.BindConfiguration("JobsGeParserOptions")
			.ValidateOnStart();
		self.AddSingleton<IValidateOptions<JobsGeParserOptions>, JobsGeParserOptionsValidator>();

		self.AddOptions<DatabaseOptions>()
			.BindConfiguration("Database")
			.ValidateOnStart();
		self.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();

		self.AddOptions<CorsOptions>()
			.BindConfiguration("Cors")
			.ValidateOnStart();

		self.AddDbContext<JobsDbContext>(dbOptions =>
			dbOptions.UseNpgsql(connectionString));

		self.AddHttpClient("JobsGeClient", (sp, c) =>
		{
			var options = sp.GetRequiredService<IOptions<JobsGeParserOptions>>().Value;
			c.BaseAddress = new Uri(options.BaseUrl);
			c.Timeout = TimeSpan.FromSeconds(options.HttpClientTimeoutSeconds);
		});

		self.AddSingleton<ScrapeWorkerState>();
		self.AddSingleton<ScrapeRequestThrottle>();
		self.AddSingleton<ScrapeBatchRunner>();

		self.AddScoped<JobsGeClient>()
			.AddSingleton<HtmlProcessor>()
			.AddSingleton<EnrichmentService>()
			.AddScoped<Repo>();

		self.AddHostedService<JobScrapeWorker>();

		var corsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
		if (corsOrigins.Length > 0)
		{
			self.AddCors(corsOptions =>
			{
				corsOptions.AddPolicy("Frontend", policy =>
				{
					policy.WithOrigins(corsOrigins)
						.AllowAnyHeader()
						.AllowAnyMethod();
				});
			});
		}

		return self;
	}
}
