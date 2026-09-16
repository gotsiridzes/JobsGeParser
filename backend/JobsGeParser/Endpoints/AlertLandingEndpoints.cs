using System.Net;
using JobsGeParser.Alerts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using JobsGeParser.Configuration;

namespace JobsGeParser.Endpoints;

/// <summary>
/// The short capability URLs that appear in messages. Served here so the whole opt in and opt out
/// flow works without a frontend; point <c>Alerts:PublicBaseUrl</c> at whichever host serves these.
///
/// Deliberately plain HTML with no JavaScript: a mail client's in-app browser must be able to
/// complete an unsubscribe, and the one-click target has to work with no page at all.
/// </summary>
public static class AlertLandingEndpoints
{
	public static void RegisterAlertLandingEndpoints(this IEndpointRouteBuilder routeBuilder)
	{
		var landing = routeBuilder.MapGroup("a")
			.WithTags("Alerts")
			.RequireRateLimiting(AlertRateLimitPolicies.Manage)
			.AddEndpointFilter(async (context, next) =>
			{
				var response = context.HttpContext.Response;
				// The token is in the path, so keep it out of referrers and caches.
				response.Headers["Referrer-Policy"] = "no-referrer";
				response.Headers.CacheControl = "no-store";
				return await next(context);
			});

		landing.MapGet("c/{token}", async (string token, AlertSubscriptionService service, CancellationToken ct) =>
			{
				var result = await service.ConfirmAsync(token, ct);

				if (result.Outcome != AlertTokenOutcome.Ok)
					return Html(Page("Link not valid", "This confirmation link is unknown or has expired. Request a new alert to get a fresh one."), StatusCodes.Status404NotFound);

				var body = $"""
					<p>Your alerts are confirmed. {result.ActivatedSubscriptions} alert(s) are now active.</p>
					<p>Keep this link to manage or stop them later:</p>
					<p><code>{WebUtility.HtmlEncode(result.ManageToken)}</code></p>
					""";

				return Html(Page("Alerts confirmed", body));
			})
			.WithName("ConfirmAlertLanding")
			.WithSummary("Confirmation link target from a confirmation message");

		landing.MapGet("u/{token}", (string token) =>
				Html(Page(
					"Unsubscribe",
					$"""
					<p>Stop all job alerts for this address?</p>
					<form method="post" action="/a/u/{WebUtility.HtmlEncode(token)}">
						<button type="submit">Unsubscribe</button>
					</form>
					""")))
			.WithName("UnsubscribeAlertLanding")
			.WithSummary("Unsubscribe confirmation page");

		landing.MapPost("u/{token}", async (string token, AlertSubscriptionService service, CancellationToken ct) =>
			{
				var outcome = await service.UnsubscribeAsync(token, null, ct);

				// RFC 8058 one-click: a mail client POSTs here directly and expects a plain 200,
				// so this must never require a form submission or a confirmation step.
				return outcome == AlertTokenOutcome.Ok
					? Html(Page("Unsubscribed", "<p>You will not receive further job alerts at this address.</p>"))
					: Html(Page("Link not valid", "<p>This link is unknown or has already been used.</p>"), StatusCodes.Status404NotFound);
			})
			.WithName("UnsubscribeAlertOneClick")
			.WithSummary("RFC 8058 one-click unsubscribe target");
	}

	private static IResult Html(string html, int statusCode = StatusCodes.Status200OK) =>
		Results.Text(html, "text/html; charset=utf-8", statusCode: statusCode);

	// Double dollar so the CSS braces below stay literal and only {{...}} interpolates.
	private static string Page(string title, string body) =>
		$$"""
		<!doctype html>
		<html lang="en">
		<head>
			<meta charset="utf-8">
			<meta name="viewport" content="width=device-width, initial-scale=1">
			<title>{{WebUtility.HtmlEncode(title)}}</title>
			<style>
				body { font: 16px/1.5 system-ui, sans-serif; margin: 0; padding: 2rem; color: #111; }
				main { max-width: 32rem; margin: 0 auto; }
				code { word-break: break-all; background: #f4f4f5; padding: .25rem .4rem; border-radius: .25rem; }
				button { font: inherit; padding: .5rem 1rem; cursor: pointer; }
			</style>
		</head>
		<body>
			<main>
				<h1>{{WebUtility.HtmlEncode(title)}}</h1>
				{{body}}
				<p><small>Job data from <a href="https://jobs.ge/">jobs.ge</a>.</small></p>
			</main>
		</body>
		</html>
		""";
}
