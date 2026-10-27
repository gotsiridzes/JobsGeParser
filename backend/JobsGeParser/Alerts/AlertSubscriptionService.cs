using JobsGeParser.Alerts.Channels;
using JobsGeParser.Alerts.Rendering;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Alerts;

/// <summary>
/// The subscribe, confirm and unsubscribe use cases. Endpoints stay thin and this holds the
/// rules, including the abuse guards that IP rate limiting cannot enforce.
/// </summary>
public sealed class AlertSubscriptionService(
	AlertRepo repo,
	AlertCriteriaValidator validator,
	AlertChannelRegistry channels,
	AlertRendererRegistry renderers,
	AlertLinkBuilder links,
	IOptions<AlertOptions> options,
	TimeProvider timeProvider,
	ILogger<AlertSubscriptionService> logger)
{
	private readonly AlertOptions _options = options.Value;

	public async Task<AlertSubscribeResult> SubscribeAsync(
		AlertChannelKind channel,
		string rawAddress,
		string name,
		AlertCadence cadence,
		TimeOnly digestSendLocalTime,
		string? timeZoneId,
		AlertCriteria criteria,
		CancellationToken ct = default)
	{
		var errors = new List<string>();

		if (!channels.Supports(channel))
			errors.Add($"Channel {channel} is not available.");
		else if (!channels.Get(channel).TryNormalizeAddress(rawAddress, out rawAddress, out var addressError))
			errors.Add(addressError!);

		if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
			errors.Add("Name is required and cannot exceed 100 characters.");

		var zone = timeZoneId ?? _options.DefaultTimeZoneId;
		if (!TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _))
			errors.Add($"Unknown timezone: {zone}");

		var knownSlugs = await repo.GetKnownCategorySlugsAsync(ct);
		errors.AddRange(validator.Validate(criteria, knownSlugs));

		if (errors.Count > 0)
			return new AlertSubscribeResult(AlertSubscribeOutcome.Invalid, Errors: errors);

		var now = timeProvider.GetUtcNow();
		var canonical = criteria.Canonicalize();
		var matching = await repo.CountMatchingAsync(canonical, ct);

		// Global cost circuit breaker, checked before anything is written.
		var confirmationsToday = await repo.CountConfirmationsSinceAsync(now.AddDays(-1), ct);
		if (confirmationsToday >= _options.MaxConfirmationsPerDay)
		{
			logger.LogWarning("Alert confirmation daily cap reached ({Cap}); rejecting subscribe requests", _options.MaxConfirmationsPerDay);
			return new AlertSubscribeResult(AlertSubscribeOutcome.RateLimited);
		}

		var subscriber = await repo.FindSubscriberAsync(channel, rawAddress, ct);

		if (subscriber is null)
		{
			subscriber = CreateSubscriber(channel, rawAddress, zone, now);
			repo.AddSubscriber(subscriber);
		}
		else if (subscriber.Status == AlertSubscriberStatus.Suppressed)
		{
			// Do not resurrect an address the provider told us is bad, and do not reveal that.
			logger.LogInformation("Subscribe request for a suppressed subscriber {SubscriberId} ignored", subscriber.Id);
			return new AlertSubscribeResult(AlertSubscribeOutcome.Accepted, matching);
		}
		else if (subscriber.Status == AlertSubscriberStatus.Unsubscribed)
		{
			// They are asking again, so put them back into the unproven state.
			subscriber.Status = AlertSubscriberStatus.Pending;
			subscriber.UnsubscribedAt = null;
			subscriber.UpdatedAt = now;
		}

		var activeCount = await repo.CountActiveSubscriptionsAsync(subscriber.Id, ct);
		if (activeCount >= _options.MaxSubscriptionsPerSubscriber)
			return new AlertSubscribeResult(AlertSubscribeOutcome.Accepted, matching);

		var hash = canonical.ComputeHash();
		var duplicate = await repo.FindDuplicateSubscriptionAsync(subscriber.Id, hash, ct);
		if (duplicate is not null)
		{
			// Someone resubmitting the same alert usually never got, or lost, the confirmation.
			// Send a fresh one, still subject to the per-address limits.
			if (subscriber.Status == AlertSubscriberStatus.Pending && CanSendConfirmation(subscriber, now))
			{
				EnqueueConfirmation(subscriber, duplicate.Name, matching, now);
				await repo.SaveChangesAsync(ct);
			}

			return new AlertSubscribeResult(AlertSubscribeOutcome.Accepted, matching);
		}

		var alreadyProven = subscriber.Status == AlertSubscriberStatus.Confirmed;

		var subscription = new AlertSubscriptionEntity
		{
			Id = Guid.CreateVersion7(),
			SubscriberId = subscriber.Id,
			Name = name.Trim(),
			Status = alreadyProven ? AlertSubscriptionStatus.Active : AlertSubscriptionStatus.PendingConfirmation,
			Cadence = cadence,
			DigestSendLocalTime = digestSendLocalTime,
			MatchFromAt = now,
			CriteriaHash = hash,
			CreatedAt = now,
			UpdatedAt = now,
			Query = canonical.Query,
			TitleContains = canonical.TitleContains,
			DescriptionContains = canonical.DescriptionContains,
			CompanyContains = canonical.CompanyContains,
			CategorySlugs = ToArray(canonical.CategorySlugs),
			Cities = ToArray(canonical.Cities),
			WorkModes = ToArray(canonical.WorkModes),
			EmploymentTypes = ToArray(canonical.EmploymentTypes),
			Seniorities = ToArray(canonical.Seniorities),
			Languages = ToArray(canonical.Languages),
			SalaryMin = canonical.SalaryMin,
			SalaryMax = canonical.SalaryMax,
			SalaryCurrency = canonical.SalaryCurrency,
			RequireSalary = canonical.RequireSalary
		};

		repo.AddSubscription(subscription);

		if (!alreadyProven && CanSendConfirmation(subscriber, now))
		{
			EnqueueConfirmation(subscriber, name.Trim(), matching, now);
		}
		else if (alreadyProven && _options.WelcomeBatchEnabled)
		{
			// Already-proven addresses skip the confirmation step entirely (a second alert on the
			// same address goes straight to Active), so this is the only place their welcome batch
			// can be triggered.
			await EnqueueWelcomeBatchAsync(subscriber, subscription, now, ct);
		}

		await repo.SaveChangesAsync(ct);

		return new AlertSubscribeResult(AlertSubscribeOutcome.Accepted, matching);
	}

	public async Task<AlertConfirmResult> ConfirmAsync(string token, CancellationToken ct = default)
	{
		if (!AlertTokens.IsWellFormed(token))
			return new AlertConfirmResult(AlertTokenOutcome.NotFound);

		var subscriber = await repo.FindByConfirmationTokenAsync(AlertTokens.Hash(token), ct);
		var now = timeProvider.GetUtcNow();

		if (subscriber is null || subscriber.ConfirmationExpiresAt < now)
			return new AlertConfirmResult(AlertTokenOutcome.NotFound);

		subscriber.Status = AlertSubscriberStatus.Confirmed;
		subscriber.ConfirmedAt ??= now;
		subscriber.ConfirmationTokenHash = null;
		subscriber.ConfirmationExpiresAt = null;
		subscriber.ConsecutiveFailures = 0;
		subscriber.UpdatedAt = now;

		var activated = 0;
		foreach (var subscription in subscriber.Subscriptions)
		{
			if (subscription.Status != AlertSubscriptionStatus.PendingConfirmation)
				continue;

			subscription.Status = AlertSubscriptionStatus.Active;
			// Stamped at confirmation, not at creation, so the clock starts when they actually
			// opted in rather than when they filled in the form.
			subscription.MatchFromAt = now;
			subscription.UpdatedAt = now;
			activated++;

			if (_options.WelcomeBatchEnabled)
				await EnqueueWelcomeBatchAsync(subscriber, subscription, now, ct);
		}

		await repo.SaveChangesAsync(ct);

		return new AlertConfirmResult(
			AlertTokenOutcome.Ok,
			AlertTokens.DeriveManageToken(subscriber.Id, _options.TokenSecret),
			activated);
	}

	public async Task<AlertSubscriberEntity?> AuthenticateAsync(string? token, CancellationToken ct = default)
	{
		if (!AlertTokens.IsWellFormed(token))
			return null;

		var subscriber = await repo.FindByManageTokenAsync(AlertTokens.Hash(token!), ct);

		return subscriber?.Status == AlertSubscriberStatus.Suppressed ? null : subscriber;
	}

	public async Task<AlertTokenOutcome> UnsubscribeAsync(
		string token,
		Guid? subscriptionId,
		CancellationToken ct = default)
	{
		if (!AlertTokens.IsWellFormed(token))
			return AlertTokenOutcome.NotFound;

		var subscriber = await repo.FindByManageTokenAsync(AlertTokens.Hash(token), ct);
		if (subscriber is null)
			return AlertTokenOutcome.NotFound;

		var now = timeProvider.GetUtcNow();

		if (subscriptionId is { } id)
		{
			var subscription = subscriber.Subscriptions.FirstOrDefault(s => s.Id == id);
			if (subscription is null)
				return AlertTokenOutcome.NotFound;

			subscription.Status = AlertSubscriptionStatus.Deleted;
			subscription.UpdatedAt = now;
		}
		else
		{
			foreach (var subscription in subscriber.Subscriptions)
			{
				subscription.Status = AlertSubscriptionStatus.Deleted;
				subscription.UpdatedAt = now;
			}

			subscriber.Status = AlertSubscriberStatus.Unsubscribed;
			subscriber.UnsubscribedAt = now;
			subscriber.UpdatedAt = now;

			// Nothing still queued should reach someone who just opted out.
			await repo.CancelQueuedMessagesAsync(subscriber.Id, ct);
		}

		await repo.SaveChangesAsync(ct);
		return AlertTokenOutcome.Ok;
	}

	private AlertSubscriberEntity CreateSubscriber(
		AlertChannelKind channel,
		string address,
		string timeZoneId,
		DateTimeOffset now)
	{
		// Version 7 keeps newly created rows adjacent in the index.
		var id = Guid.CreateVersion7();

		return new AlertSubscriberEntity
		{
			Id = id,
			Channel = channel,
			Address = address,
			Status = AlertSubscriberStatus.Pending,
			TimeZoneId = timeZoneId,
			ManageTokenHash = AlertTokens.Hash(AlertTokens.DeriveManageToken(id, _options.TokenSecret)),
			CreatedAt = now,
			UpdatedAt = now
		};
	}

	/// <summary>
	/// Per-address guard, because IP rate limiting is trivially bypassed and this endpoint sends
	/// to whatever address the caller typed.
	/// </summary>
	private bool CanSendConfirmation(AlertSubscriberEntity subscriber, DateTimeOffset now)
	{
		if (subscriber.ConfirmationSentAt is { } lastSent)
		{
			if (now - lastSent < TimeSpan.FromSeconds(60))
				return false;

			if (now - lastSent < TimeSpan.FromDays(1)
				&& subscriber.ConfirmationSendCount >= _options.MaxConfirmationsPerAddressPerDay)
			{
				return false;
			}

			if (now - lastSent >= TimeSpan.FromDays(1))
				subscriber.ConfirmationSendCount = 0;
		}

		return true;
	}

	private void EnqueueConfirmation(
		AlertSubscriberEntity subscriber,
		string subscriptionName,
		int matchingJobs,
		DateTimeOffset now)
	{
		var token = AlertTokens.CreateRandom();
		subscriber.ConfirmationTokenHash = AlertTokens.Hash(token);
		subscriber.ConfirmationSentAt = now;
		subscriber.ConfirmationExpiresAt = now.AddHours(_options.ConfirmationTokenTtlHours);
		subscriber.ConfirmationSendCount++;

		var rendered = renderers.Get(subscriber.Channel).RenderConfirmation(
			new AlertConfirmationModel(links.Confirm(token), subscriptionName, matchingJobs));

		repo.AddMessage(new AlertMessageEntity
		{
			Id = Guid.CreateVersion7(),
			SubscriberId = subscriber.Id,
			Kind = AlertMessageKind.Confirmation,
			Channel = subscriber.Channel,
			Address = subscriber.Address,
			// One confirmation per send window, so a retry of the same request cannot double send.
			IdempotencyKey = $"confirm:{subscriber.Id}:{now.ToUnixTimeSeconds()}",
			Status = AlertMessageStatus.Pending,
			Subject = rendered.Subject,
			BodyText = rendered.BodyText,
			BodyHtml = rendered.BodyHtml,
			AvailableAt = now,
			CreatedAt = now
		});
	}

	/// <summary>
	/// Sends one immediate email with the newest jobs already matching this filter, capped the
	/// same as a daily digest, so a new subscriber sees the alert work today rather than waiting
	/// for the next new posting. Fully independent of the subscription's own cadence: an Instant
	/// alert still gets one combined welcome email, never one per already-existing match.
	///
	/// Deliberately a one-time top-N rather than a full backlog dump: jobs beyond the cap are
	/// simply never sent, matching the render-cap behaviour of a normal digest overflow. They are
	/// also unreachable later, because the reverse-match path only considers jobs whose
	/// FirstSeenAt is at or after MatchFromAt, and every backlog job here is older than that.
	/// </summary>
	private async Task EnqueueWelcomeBatchAsync(
		AlertSubscriberEntity subscriber,
		AlertSubscriptionEntity subscription,
		DateTimeOffset now,
		CancellationToken ct)
	{
		var cap = subscriber.Channel == AlertChannelKind.Sms
			? _options.Sms.MaxDigestItems
			: _options.Email.MaxDigestItems;

		var (jobs, totalCount) = await repo.GetWelcomeBatchAsync(AlertCriteria.From(subscription), cap, ct);
		if (jobs.Count == 0)
			return;

		var manageToken = AlertTokens.DeriveManageToken(subscriber.Id, _options.TokenSecret);

		var rendered = renderers.Get(subscriber.Channel).RenderDigest(new AlertDigestModel(
			subscription.Name,
			[.. jobs.Select(AlertJobModel.FromEntity)],
			totalCount,
			links.Manage(manageToken),
			links.Unsubscribe(manageToken)));

		var messageId = Guid.CreateVersion7();

		repo.AddMessage(new AlertMessageEntity
		{
			Id = messageId,
			SubscriberId = subscriber.Id,
			SubscriptionId = subscription.Id,
			Kind = AlertMessageKind.WelcomeBatch,
			Channel = subscriber.Channel,
			Address = subscriber.Address,
			// One welcome batch per subscription, ever, however many times a lost confirmation
			// link gets replayed.
			IdempotencyKey = $"welcome:{subscription.Id}",
			Status = AlertMessageStatus.Pending,
			Subject = rendered.Subject,
			BodyText = rendered.BodyText,
			BodyHtml = rendered.BodyHtml,
			AvailableAt = now,
			CreatedAt = now
		});

		foreach (var job in jobs)
		{
			repo.AddDelivery(new AlertDeliveryEntity
			{
				SubscriptionId = subscription.Id,
				JobId = job.Id,
				MatchedAt = now,
				Status = AlertDeliveryStatus.Queued,
				MessageId = messageId
			});
		}

		logger.LogInformation(
			"Queued welcome batch for subscription {SubscriptionId} with {JobCount} of {TotalCount} matching job(s)",
			subscription.Id,
			jobs.Count,
			totalCount);
	}

	private static string[]? ToArray(IReadOnlyList<string>? values) => values?.ToArray();
}
