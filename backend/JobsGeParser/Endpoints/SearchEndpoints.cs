using JobsGeParser.Configuration;
using JobsGeParser.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Endpoints;

public static class SearchEndpoints
{
	public static void RegisterSearchEndpoints(this IEndpointRouteBuilder routeBuilder)
	{
		var search = routeBuilder.MapGroup("api/search")
			.WithTags("Search");

		search.MapGet("", async (
				Repo repo,
				IOptions<JobsGeParserOptions> options,
				string? q,
				string? category,
				int? page,
				int? pageSize,
				CancellationToken ct) =>
			{
				if (string.IsNullOrWhiteSpace(q))
					return Results.BadRequest(new { error = "Query parameter 'q' is required." });

				var result = await repo.SearchJobsAsync(
					q,
					category,
					page ?? 1,
					pageSize ?? options.Value.DefaultJobsPageSize,
					ct);
				return Results.Ok(result);
			})
			.WithName("SearchJobs")
			.WithSummary("Ranked full-text search over job title, company, and description");
	}
}