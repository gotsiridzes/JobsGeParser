using JobsGeParser.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JobsGeParser.Endpoints;

public static class Jobs
{
	public static void RegisterJobsEndpoints(this IEndpointRouteBuilder routeBuilder)
	{
		var jobs = routeBuilder.MapGroup("api/jobs/");

		jobs.MapGet("categories", async (Repo repo, CancellationToken ct) =>
			Results.Ok(await repo.GetCategoriesAsync(ct)));

		jobs.MapGet("", async (Repo repo, string? category, CancellationToken ct) =>
			Results.Ok(await repo.GetJobsAsync(category, ct)));

		jobs.MapGet("dotnet", async (Repo repo, string? category, CancellationToken ct) =>
			Results.Ok(await repo.ListDotnetApplicationsAsync(category, ct)));
	}
}
