using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JobsGeParser.Endpoints;

public static class Subscriptions
{
	public static void RegisterJobsEndpoints(this IEndpointRouteBuilder routeBuilder)
	{
		var jobs = routeBuilder.MapGroup("api/jobs/");
		var channel = Channel.CreateBounded<JobApplication>(20);

		jobs.MapPost("retrieve", async (JobsGeClient client) =>
		{
			await client.RetrievePageItemsAsync(channel);

			return Results.Ok();
		});

		jobs.MapGet("", async (Repo repo) => Results.Ok(repo.GetProcessedApplications()));
		jobs.MapGet("dotnet", async (Repo repo) => Results.Ok(repo.ListDotnetApplications()));
	}
}