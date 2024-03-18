using JobsGeParser;
using JobsGeParser.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

var ops = new JobsGeParserOptions();
builder.Configuration.Bind("JobsGeParserOptions", ops);

builder.Services.AddJobsGeService(ops);

var app = builder.Build();

app.RegisterJobsEndpoints();

app.Run();