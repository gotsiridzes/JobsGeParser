using JobsGeParser.Configuration;
using JobsGeParser.Data;
using JobsGeParser.Endpoints;
using JobsGeParser.Scraping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

var ops = new JobsGeParserOptions();
builder.Configuration.Bind("JobsGeParserOptions", ops);

var databaseOptions = new DatabaseOptions();
builder.Configuration.Bind("Database", databaseOptions);

var connectionString = builder.Configuration.GetConnectionString("JobsGeParser")
	?? throw new InvalidOperationException("Connection string 'JobsGeParser' is not configured.");

builder.Services.AddJobsGeService(ops, connectionString);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();

	if (databaseOptions.AutoMigrate)
		db.Database.Migrate();

	await CategorySync.SyncAsync(db, ops);
}

app.RegisterJobsEndpoints();
app.RegisterScrapeEndpoints();

app.Run();
