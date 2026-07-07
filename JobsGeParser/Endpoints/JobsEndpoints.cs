using JobsGeParser.Configuration;
using JobsGeParser.Data;
using JobsGeParser.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JobsGeParser.Endpoints;

public static class JobsEndpoints
{
	public static void RegisterJobsEndpoints(this IEndpointRouteBuilder routeBuilder)
	{
		var jobs = routeBuilder.MapGroup("api/jobs/");

		jobs.MapGet("categories", async (Repo repo, CancellationToken ct) =>
			Results.Ok(await repo.GetCategoriesAsync(ct)));

		jobs.MapGet("", async (
			Repo repo,
			JobsGeParserOptions options,
			string? category,
			string? q,
			int? page,
			int? pageSize,
			CancellationToken ct) =>
			Results.Ok(await repo.GetJobsPageAsync(
				new JobQuery(category, q, DotNetOnly: false),
				page ?? 1,
				pageSize ?? options.DefaultJobsPageSize,
				ct)));

		jobs.MapGet("dotnet", async (
			Repo repo,
			JobsGeParserOptions options,
			string? category,
			string? q,
			int? page,
			int? pageSize,
			CancellationToken ct) =>
			Results.Ok(await repo.GetJobsPageAsync(
				new JobQuery(category, q, DotNetOnly: true),
				page ?? 1,
				pageSize ?? options.DefaultJobsPageSize,
				ct)));

		jobs.MapGet("{id:int}", async (Repo repo, int id, CancellationToken ct) =>
		{
			var job = await repo.GetJobByIdAsync(id, ct);
			return job is null ? Results.NotFound() : Results.Ok(job);
		});
	}
}
