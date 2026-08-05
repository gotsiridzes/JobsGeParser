using JobsGeParser.Configuration;
using JobsGeParser.Data;
using JobsGeParser.Endpoints;
using JobsGeParser.Scraping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJobsGeService(builder.Configuration);

if (builder.Environment.IsDevelopment())
	builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
	app.MapOpenApi();

var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("JobsGeParser.Startup");
var ops = app.Services.GetRequiredService<IOptions<JobsGeParserOptions>>().Value;
var databaseOptions = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
var corsOptions = app.Services.GetRequiredService<IOptions<CorsOptions>>().Value;

logger.LogInformation("Starting JobsGeParser");

using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();

	if (databaseOptions.AutoMigrate)
	{
		logger.LogInformation("Applying database migrations");
		db.Database.Migrate();
		logger.LogInformation("Database migrations applied");
	}

	logger.LogInformation("Syncing categories from configuration");
	await CategorySync.SyncAsync(db, ops, logger);
}

if (corsOptions.AllowedOrigins.Length > 0)
	app.UseCors("Frontend");

app.RegisterJobsEndpoints();
app.RegisterSearchEndpoints();
app.RegisterScrapeEndpoints();

logger.LogInformation(
	"JobsGeParser ready: scrape {ScrapeState}, interval {IntervalMinutes} min, {EnabledCategories} enabled categories",
	ops.ScrapeEnabled ? "enabled" : "disabled",
	ops.ScrapeIntervalMinutes,
	ops.EnabledCategories.Count());

app.Run();
