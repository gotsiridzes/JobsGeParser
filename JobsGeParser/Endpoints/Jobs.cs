using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JobsGeParser.Endpoints;

public static class Jobs
{
	public static void RegisterJobsEndpoints(this IEndpointRouteBuilder routeBuilder)
	{
		var jobs = routeBuilder.MapGroup("api/jobs/");

		jobs.MapGet("", async (Repo repo, CancellationToken ct) =>
			Results.Ok(await repo.GetProcessedApplicationsAsync(ct)));

		jobs.MapGet("dotnet", async (Repo repo, CancellationToken ct) =>
			Results.Ok(await repo.ListDotnetApplicationsAsync(ct)));

		jobs.MapGet("scrape/status", async (Repo repo, CancellationToken ct) =>
		{
			var run = await repo.GetLatestScrapeRunAsync(ct);
			return run is null ? Results.NotFound() : Results.Ok(run);
		});
	}
}
