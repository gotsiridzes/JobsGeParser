using JobsGeParser;
using JobsGeParser.Data;
using JobsGeParser.Endpoints;
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

if (databaseOptions.AutoMigrate)
{
	using var scope = app.Services.CreateScope();
	var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
	db.Database.Migrate();
}

app.RegisterJobsEndpoints();

app.Run();
