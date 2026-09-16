using JobsGeParser.Alerts;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using JobsGeParser.Endpoints.Dtos;
using JobsGeParser.Enrichment;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Endpoints;

public static class AlertEndpoints
{
	/// <summary>
	/// The capability token travels in a header for JSON calls, never a query string, so it does
	/// not leak through referrers, proxy logs or browser history.
	/// </summary>
	public const string TokenHeader = "X-Alert-Token";

	public static void RegisterAlertEndpoints(this IEndpointRouteBuilder routeBuilder)
	{
		var alerts = routeBuilder.MapGroup("api/alerts").WithTags("Alerts");

		RegisterDiscovery(alerts);
		RegisterSubscribe(alerts);
		RegisterManagement(alerts);
	}

	private static void RegisterDiscovery(RouteGroupBuilder alerts)
	{
		alerts.MapGet("criteria-options", async (AlertRepo alertRepo, CancellationToken ct) =>
			{
				var slugs = await alertRepo.GetKnownCategorySlugsAsync(ct);

				return Results.Ok(new AlertCriteriaOptionsDto(
					Sorted(slugs),
					Sorted(EnrichmentService.CityValues),
					Sorted(EnrichmentService.WorkModeValues),
					Sorted(EnrichmentService.EmploymentTypeValues),
					Sorted(EnrichmentService.SeniorityValues),
					Sorted(EnrichmentService.LanguageValues),
					AlertCriteriaValidator.MaxTextLength,
					AlertCriteriaValidator.MaxArrayLength));
			})
			.WithName("GetAlertCriteriaOptions")
			.WithSummary("Values an alert filter may use for each faceted criterion");

		alerts.MapPost("preview", async (
				AlertPreviewRequest request,
				AlertRepo alertRepo,
				AlertCriteriaValidator validator,
				IOptions<JobsGeParserOptions> options,
				CancellationToken ct) =>
			{
				var criteria = request.Criteria.ToCriteria();
				var knownSlugs = await alertRepo.GetKnownCategorySlugsAsync(ct);

				var errors = validator.Validate(criteria, knownSlugs);
				if (errors.Count > 0)
					return Results.BadRequest(new AlertValidationErrorDto(errors));

				var result = await alertRepo.PreviewAsync(
					criteria,
					request.Page ?? 1,
					request.PageSize ?? options.Value.DefaultJobsPageSize,
					ct);

				return Results.Ok(result);
			})
			.RequireRateLimiting(AlertRateLimitPolicies.Preview)
			.WithName("PreviewAlertCriteria")
			.WithSummary("Jobs an alert filter matches right now, without subscribing to anything");
	}

	private static void RegisterSubscribe(RouteGroupBuilder alerts)
	{
		alerts.MapPost("subscriptions", async (
				CreateAlertSubscriptionRequest request,
				AlertSubscriptionService service,
				CancellationToken ct) =>
			{
				if (!Enum.TryParse<AlertChannelKind>(request.Channel, ignoreCase: true, out var channel))
					return Results.BadRequest(new AlertValidationErrorDto([$"Unknown channel: {request.Channel}"]));

				if (!Enum.TryParse<AlertCadence>(request.Cadence, ignoreCase: true, out var cadence))
					return Results.BadRequest(new AlertValidationErrorDto([$"Unknown cadence: {request.Cadence}"]));

				var result = await service.SubscribeAsync(
					channel,
					request.Address ?? "",
					request.Name ?? "",
					cadence,
					request.DigestSendLocalTime ?? new TimeOnly(9, 0),
					request.TimeZoneId,
					request.Criteria.ToCriteria(),
					ct);

				return result.Outcome switch
				{
					AlertSubscribeOutcome.Invalid =>
						Results.BadRequest(new AlertValidationErrorDto(result.Errors ?? [])),
					AlertSubscribeOutcome.RateLimited =>
						Results.StatusCode(StatusCodes.Status503ServiceUnavailable),
					_ => Results.Accepted(
						value: new CreateAlertSubscriptionResponse("confirmation_sent", result.CurrentlyMatchingJobs))
				};
			})
			.RequireRateLimiting(AlertRateLimitPolicies.Subscribe)
			.WithName("CreateAlertSubscription")
			.WithSummary("Create an alert subscription and send a confirmation to the given address");

		alerts.MapPost("confirm", async (
				ConfirmAlertRequest request,
				AlertSubscriptionService service,
				AlertLinkBuilder links,
				CancellationToken ct) =>
			{
				var result = await service.ConfirmAsync(request.Token ?? "", ct);

				if (result.Outcome != AlertTokenOutcome.Ok)
					return NotFound();

				return Results.Ok(new ConfirmAlertResponse(
					result.ManageToken!,
					links.Manage(result.ManageToken!),
					result.ActivatedSubscriptions));
			})
			.RequireRateLimiting(AlertRateLimitPolicies.Manage)
			.WithName("ConfirmAlertSubscription")
			.WithSummary("Prove ownership of an address and activate its pending subscriptions");

		alerts.MapPost("unsubscribe", async (
				UnsubscribeAlertRequest request,
				AlertSubscriptionService service,
				CancellationToken ct) =>
			{
				var outcome = await service.UnsubscribeAsync(request.Token ?? "", request.SubscriptionId, ct);

				return outcome == AlertTokenOutcome.Ok ? Results.NoContent() : NotFound();
			})
			.RequireRateLimiting(AlertRateLimitPolicies.Manage)
			.WithName("UnsubscribeAlerts")
			.WithSummary("Stop one subscription, or every subscription for this address");
	}

	private static void RegisterManagement(RouteGroupBuilder alerts)
	{
		var managed = alerts.MapGroup("subscriptions")
			.RequireRateLimiting(AlertRateLimitPolicies.Manage);

		managed.MapGet("", async (
				HttpContext context,
				AlertSubscriptionService service,
				AlertRepo repo,
				CancellationToken ct) =>
			{
				var subscriber = await service.AuthenticateAsync(Token(context), ct);
				if (subscriber is null)
					return NotFound();

				var subscriptions = await repo.GetSubscriptionsAsync(subscriber.Id, ct);
				var stats = await repo.GetSubscriptionStatsAsync(subscriber.Id, ct);

				return Results.Ok(new AlertSubscriptionsResponse(
					subscriber.Channel.ToString(),
					AlertAddressMasking.Mask(subscriber.Channel, subscriber.Address),
					subscriber.Status,
					[.. subscriptions.Select(s => ToDto(s, subscriber, stats))]));
			})
			.WithName("GetAlertSubscriptions")
			.WithSummary("Subscriptions belonging to the capability token in the request header");

		managed.MapPatch("{id:guid}", async (
				Guid id,
				UpdateAlertSubscriptionRequest request,
				HttpContext context,
				AlertSubscriptionService service,
				AlertRepo repo,
				AlertCriteriaValidator validator,
				CancellationToken ct) =>
			{
				var subscriber = await service.AuthenticateAsync(Token(context), ct);
				if (subscriber is null)
					return NotFound();

				var subscription = await repo.FindSubscriptionAsync(subscriber.Id, id, ct);
				if (subscription is null)
					return NotFound();

				if (request.Cadence is { } rawCadence)
				{
					if (!Enum.TryParse<AlertCadence>(rawCadence, ignoreCase: true, out var cadence))
						return Results.BadRequest(new AlertValidationErrorDto([$"Unknown cadence: {rawCadence}"]));

					subscription.Cadence = cadence;
				}

				if (request.Name is { } name)
				{
					if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
						return Results.BadRequest(new AlertValidationErrorDto(["Name is required and cannot exceed 100 characters."]));

					subscription.Name = name.Trim();
				}

				if (request.DigestSendLocalTime is { } sendAt)
					subscription.DigestSendLocalTime = sendAt;

				if (request.Criteria is { } criteriaDto)
				{
					var criteria = criteriaDto.ToCriteria().Canonicalize();
					var errors = validator.Validate(criteria, await repo.GetKnownCategorySlugsAsync(ct));
					if (errors.Count > 0)
						return Results.BadRequest(new AlertValidationErrorDto(errors));

					ApplyCriteria(subscription, criteria);
				}

				subscription.UpdatedAt = DateTimeOffset.UtcNow;
				await repo.SaveChangesAsync(ct);

				return Results.NoContent();
			})
			.WithName("UpdateAlertSubscription")
			.WithSummary("Rename a subscription, change its cadence, or replace its filter");

		managed.MapPost("{id:guid}/pause", (Guid id, HttpContext context, AlertSubscriptionService service, AlertRepo repo, CancellationToken ct) =>
				SetStatusAsync(id, context, service, repo, AlertSubscriptionStatus.Paused, resetMatchWindow: false, ct))
			.WithName("PauseAlertSubscription")
			.WithSummary("Stop alerting on this subscription without deleting it");

		managed.MapPost("{id:guid}/resume", (Guid id, HttpContext context, AlertSubscriptionService service, AlertRepo repo, CancellationToken ct) =>
				SetStatusAsync(id, context, service, repo, AlertSubscriptionStatus.Active, resetMatchWindow: true, ct))
			.WithName("ResumeAlertSubscription")
			.WithSummary("Resume alerting, starting from now rather than replaying the backlog");

		managed.MapDelete("{id:guid}", (Guid id, HttpContext context, AlertSubscriptionService service, AlertRepo repo, CancellationToken ct) =>
				SetStatusAsync(id, context, service, repo, AlertSubscriptionStatus.Deleted, resetMatchWindow: false, ct))
			.WithName("DeleteAlertSubscription")
			.WithSummary("Delete one subscription");
	}

	private static async Task<IResult> SetStatusAsync(
		Guid id,
		HttpContext context,
		AlertSubscriptionService service,
		AlertRepo repo,
		string status,
		bool resetMatchWindow,
		CancellationToken ct)
	{
		var subscriber = await service.AuthenticateAsync(Token(context), ct);
		if (subscriber is null)
			return NotFound();

		var subscription = await repo.FindSubscriptionAsync(subscriber.Id, id, ct);
		if (subscription is null)
			return NotFound();

		var now = DateTimeOffset.UtcNow;
		subscription.Status = status;
		subscription.UpdatedAt = now;

		// Resuming starts the match window at now, so a subscription paused for a month does not
		// dump a month of backlog the moment it comes back.
		if (resetMatchWindow)
			subscription.MatchFromAt = now;

		await repo.SaveChangesAsync(ct);
		return Results.NoContent();
	}

	private static void ApplyCriteria(AlertSubscriptionEntity subscription, AlertCriteria criteria)
	{
		subscription.Query = criteria.Query;
		subscription.TitleContains = criteria.TitleContains;
		subscription.DescriptionContains = criteria.DescriptionContains;
		subscription.CompanyContains = criteria.CompanyContains;
		subscription.CategorySlugs = criteria.CategorySlugs?.ToArray();
		subscription.Cities = criteria.Cities?.ToArray();
		subscription.WorkModes = criteria.WorkModes?.ToArray();
		subscription.EmploymentTypes = criteria.EmploymentTypes?.ToArray();
		subscription.Seniorities = criteria.Seniorities?.ToArray();
		subscription.Languages = criteria.Languages?.ToArray();
		subscription.SalaryMin = criteria.SalaryMin;
		subscription.SalaryMax = criteria.SalaryMax;
		subscription.SalaryCurrency = criteria.SalaryCurrency;
		subscription.RequireSalary = criteria.RequireSalary;
		subscription.CriteriaHash = criteria.ComputeHash();
	}

	private static AlertSubscriptionDto ToDto(
		AlertSubscriptionEntity subscription,
		AlertSubscriberEntity subscriber,
		IReadOnlyDictionary<Guid, (int Sent, DateTimeOffset? LastSentAt)> stats)
	{
		stats.TryGetValue(subscription.Id, out var stat);

		return new AlertSubscriptionDto(
			subscription.Id,
			subscription.Name,
			subscription.Status,
			subscription.Cadence.ToString(),
			subscription.DigestSendLocalTime,
			subscriber.Channel.ToString(),
			AlertAddressMasking.Mask(subscriber.Channel, subscriber.Address),
			subscription.CreatedAt,
			subscription.MatchFromAt,
			stat.LastSentAt,
			stat.Sent,
			AlertCriteriaDto.From(AlertCriteria.From(subscription)));
	}

	private static string? Token(HttpContext context) =>
		context.Request.Headers.TryGetValue(TokenHeader, out var values) ? values.ToString() : null;

	/// <summary>
	/// One uniform response for every unknown, expired or unauthorized token, so a caller cannot
	/// tell those cases apart or enumerate subscriptions.
	/// </summary>
	private static IResult NotFound() => Results.NotFound(new { error = "Not found." });

	private static IReadOnlyList<string> Sorted(IEnumerable<string> values) =>
		[.. values.OrderBy(v => v, StringComparer.OrdinalIgnoreCase)];
}
