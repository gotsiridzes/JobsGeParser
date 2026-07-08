using JobsGeParser.Configuration;
using JobsGeParser.Data;
using JobsGeParser.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Endpoints;

public static class JobsEndpoints
{
	public static void RegisterJobsEndpoints(this IEndpointRouteBuilder routeBuilder)
	{
		var jobs = routeBuilder.MapGroup("api/jobs/")
			.WithTags("Jobs");

		jobs.MapGet("categories", async (Repo repo, CancellationToken ct) =>
			Results.Ok(await repo.GetCategoriesAsync(ct)))
			.WithName("GetJobCategories")
			.WithSummary("List categories with job counts and latest scrape run");

		jobs.MapGet("", async (
			Repo repo,
			IOptions<JobsGeParserOptions> options,
			string? category,
			string? q,
			int? page,
			int? pageSize,
			CancellationToken ct) =>
			Results.Ok(await repo.GetJobsPageAsync(
				new JobQuery(category, q, DotNetOnly: false),
				page ?? 1,
				pageSize ?? options.Value.DefaultJobsPageSize,
				ct)))
			.WithName("GetJobs")
			.WithSummary("Paginated job list with optional category and search filters");

		jobs.MapGet("dotnet", async (
			Repo repo,
			IOptions<JobsGeParserOptions> options,
			string? category,
			string? q,
			int? page,
			int? pageSize,
			CancellationToken ct) =>
			Results.Ok(await repo.GetJobsPageAsync(
				new JobQuery(category, q, DotNetOnly: true),
				page ?? 1,
				pageSize ?? options.Value.DefaultJobsPageSize,
				ct)))
			.WithName("GetDotNetJobs")
			.WithSummary("Paginated .NET job list with optional category and search filters");

		jobs.MapGet("{id:int}", async (Repo repo, int id, CancellationToken ct) =>
		{
			var job = await repo.GetJobByIdAsync(id, ct);
			return job is null ? Results.NotFound() : Results.Ok(job);
		})
			.WithName("GetJobById")
			.WithSummary("Get a single job with full description and category slugs");
	}
}
