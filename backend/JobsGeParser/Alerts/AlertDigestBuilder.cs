using JobsGeParser.Alerts.Rendering;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Alerts;

/// <summary>
/// Batches pending matches into one message per subscription per local day.
///
/// The digest never re-runs filters. Matching already happened once, at a well defined moment,
/// and the ledger holds stable (subscription, job) pairs, so a later re-enrichment cannot change
/// what was already matched.
/// </summary>
public sealed class AlertDigestBuilder(
	IServiceScopeFactory scopeFactory,
	AlertRendererRegistry renderers,
	AlertLinkBuilder links,
	IOptions<AlertOptions> options,
	TimeProvider timeProvider,
	ILogger<AlertDigestBuilder> logger)
{
	private const int CandidateLimit = 500;

	private readonly AlertOptions _options = options.Value;

	/// <summary>
	/// Enqueues every digest that is due right now. Returns how many were sent.
	/// </summary>
	public async Task<int> BuildDueDigestsAsync(CancellationToken ct = default)
	{
		using var scope = scopeFactory.CreateScope();
		var repo = scope.ServiceProvider.GetRequiredService<AlertRepo>();

		var candidates = await repo.GetDigestCandidatesAsync(CandidateLimit, ct);
		if (candidates.Count == 0)
			return 0;

		var subscribers = await repo.GetSubscribersAsync([.. candidates.Select(s => s.SubscriberId).Distinct()], ct);
		var built = 0;

		foreach (var subscription in candidates)
		{
			if (ct.IsCancellationRequested)
				break;

			if (!subscribers.TryGetValue(subscription.SubscriberId, out var subscriber))
				continue;

			if (subscriber.Status != AlertSubscriberStatus.Confirmed)
				continue;

			if (!IsDue(subscription, subscriber, out var localDate))
				continue;

			try
			{
				if (await BuildDigestAsync(repo, subscription, subscriber, localDate, ct))
					built++;
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				logger.LogError(ex, "Failed to build digest for subscription {SubscriptionId}", subscription.Id);
			}
		}

		return built;
	}

	/// <summary>
	/// Due when the subscriber's local wall clock has passed the send time and no digest has gone
	/// out on their local calendar date yet. Comparing local dates rather than elapsed hours is
	/// what makes a 23 or 25 hour DST day still produce exactly one digest.
	/// </summary>
	private bool IsDue(
		AlertSubscriptionEntity subscription,
		AlertSubscriberEntity subscriber,
		out DateOnly localDate)
	{
		localDate = default;

		if (!TimeZoneInfo.TryFindSystemTimeZoneById(subscriber.TimeZoneId, out var zone))
		{
			logger.LogWarning(
				"Subscriber {SubscriberId} has an unknown timezone {TimeZoneId}; using {Fallback}",
				subscriber.Id,
				subscriber.TimeZoneId,
				_options.DefaultTimeZoneId);

			if (!TimeZoneInfo.TryFindSystemTimeZoneById(_options.DefaultTimeZoneId, out zone))
				return false;
		}

		var localNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone);
		localDate = DateOnly.FromDateTime(localNow.DateTime);

		if (TimeOnly.FromDateTime(localNow.DateTime) < subscription.DigestSendLocalTime)
			return false;

		return subscription.LastDigestSentOn is null || subscription.LastDigestSentOn < localDate;
	}

	private async Task<bool> BuildDigestAsync(
		AlertRepo repo,
		AlertSubscriptionEntity subscription,
		AlertSubscriberEntity subscriber,
		DateOnly localDate,
		CancellationToken ct)
	{
		var pending = await repo.GetPendingDeliveriesAsync(subscription.Id, ct);
		if (pending.Count == 0)
			return false;

		var cap = subscriber.Channel == AlertChannelKind.Sms
			? _options.Sms.MaxDigestItems
			: _options.Email.MaxDigestItems;

		var now = timeProvider.GetUtcNow();
		var manageToken = AlertTokens.DeriveManageToken(subscriber.Id, _options.TokenSecret);

		var rendered = renderers.Get(subscriber.Channel).RenderDigest(new AlertDigestModel(
			subscription.Name,
			[.. pending.Take(cap).Select(p => ToJobModel(p.Job))],
			pending.Count,
			links.Manage(manageToken),
			links.Unsubscribe(manageToken)));

		var messageId = Guid.CreateVersion7();

		repo.AddMessage(new AlertMessageEntity
		{
			Id = messageId,
			SubscriberId = subscriber.Id,
			SubscriptionId = subscription.Id,
			Kind = AlertMessageKind.DailyDigest,
			Channel = subscriber.Channel,
			Address = subscriber.Address,
			// The local date in the key is the second guard against a double digest, alongside
			// LastDigestSentOn. Either one alone would do; together a clock wobble cannot cause one.
			IdempotencyKey = $"digest:{subscription.Id}:{localDate:yyyy-MM-dd}",
			Status = AlertMessageStatus.Pending,
			Subject = rendered.Subject,
			BodyText = rendered.BodyText,
			BodyHtml = rendered.BodyHtml,
			AvailableAt = now,
			CreatedAt = now
		});

		for (var i = 0; i < pending.Count; i++)
		{
			var delivery = pending[i].Delivery;
			delivery.MessageId = messageId;

			// Items past the render cap are closed out rather than left pending, so a backlog
			// cannot snowball into tomorrow's digest forever.
			if (i < cap)
			{
				delivery.Status = AlertDeliveryStatus.Queued;
			}
			else
			{
				delivery.Status = AlertDeliveryStatus.Skipped;
				delivery.SkipReason = AlertDeliverySkipReason.DigestOverflow;
			}
		}

		subscription.LastDigestSentOn = localDate;
		subscription.UpdatedAt = now;

		await repo.SaveChangesAsync(ct);

		logger.LogInformation(
			"Queued digest for subscription {SubscriptionId} with {JobCount} job(s) for local date {LocalDate}",
			subscription.Id,
			pending.Count,
			localDate);

		return true;
	}

	private static AlertJobModel ToJobModel(JobEntity job) => new(
		job.Id,
		job.Name,
		job.Company,
		job.Link,
		job.City,
		job.WorkMode,
		job.Seniority,
		job.SalaryMin,
		job.SalaryMax,
		job.SalaryCurrency);
}
