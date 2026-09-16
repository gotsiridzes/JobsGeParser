using JobsGeParser.Alerts.Rendering;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Alerts;

/// <summary>
/// Drains the job event outbox: for each new job, work out which subscriptions match it, record
/// the match, and enqueue a message for the instant ones. This is the only place matching runs.
/// Digest subscriptions get a delivery row too and simply wait for the scheduler, so there is one
/// matching path and two delivery paths.
/// </summary>
public sealed class AlertOutboxDrainer(
	IServiceScopeFactory scopeFactory,
	AlertRendererRegistry renderers,
	AlertLinkBuilder links,
	IOptions<AlertOptions> options,
	TimeProvider timeProvider,
	ILogger<AlertOutboxDrainer> logger)
{
	private readonly AlertOptions _options = options.Value;

	/// <summary>
	/// Claims and processes one batch. Returns how many events were handled.
	/// </summary>
	public async Task<int> DrainOnceAsync(CancellationToken ct = default)
	{
		using var scope = scopeFactory.CreateScope();
		var repo = scope.ServiceProvider.GetRequiredService<AlertRepo>();

		var released = await repo.ReleaseStaleEventClaimsAsync(ct);
		if (released > 0)
			logger.LogWarning("Released {Count} stale job event claim(s)", released);

		await using var transaction = await repo.BeginTransactionAsync(ct);

		var events = await repo.ClaimJobEventsAsync(_options.OutboxBatchSize, ct);
		if (events.Count == 0)
			return 0;

		var now = timeProvider.GetUtcNow();

		// Several events can name the same job in one batch, for example a detail fetch and a
		// late category link. Matching the job once is enough: the ledger makes the rest free.
		foreach (var jobId in events.Select(e => e.JobId).Distinct())
		{
			try
			{
				await ProcessJobAsync(repo, jobId, now, ct);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				logger.LogError(ex, "Alert matching failed for job {JobId}", jobId);

				foreach (var failed in events.Where(e => e.JobId == jobId))
					failed.LastError = ex.Message;
			}
		}

		MarkProcessed(events, now);

		await repo.SaveChangesAsync(ct);
		await transaction.CommitAsync(ct);

		return events.Count;
	}

	private async Task ProcessJobAsync(AlertRepo repo, int jobId, DateTimeOffset now, CancellationToken ct)
	{
		var job = await repo.GetJobSnapshotAsync(jobId, ct);
		if (job is null)
		{
			// The job was deleted between the event and this drain. Nothing to alert about.
			return;
		}

		var candidates = await repo.FindCandidateSubscriptionsAsync(job, ct);
		if (candidates.Count == 0)
			return;

		// Substring and salary criteria are decided here, with no I/O.
		var matched = candidates
			.Where(s => AlertCriteriaMatcher.Matches(job, AlertCriteria.From(s)))
			.ToList();

		if (matched.Count == 0)
			return;

		// Only the free text criterion needs the database, and only for the subscriptions that
		// actually set one.
		var needFullText = matched.Where(s => !string.IsNullOrWhiteSpace(s.Query)).ToList();
		if (needFullText.Count > 0)
		{
			var passed = await repo.FilterByFullTextAsync(jobId, [.. needFullText.Select(s => s.Id)], ct);
			matched = matched
				.Where(s => string.IsNullOrWhiteSpace(s.Query) || passed.Contains(s.Id))
				.ToList();
		}

		if (matched.Count == 0)
			return;

		var newlyMatched = await repo.InsertDeliveriesAsync(jobId, [.. matched.Select(s => s.Id)], ct);
		if (newlyMatched.Count == 0)
			return;

		var instant = matched
			.Where(s => newlyMatched.Contains(s.Id) && s.Cadence == AlertCadence.Instant)
			.ToList();

		if (instant.Count > 0)
			await EnqueueInstantAsync(repo, instant, job, now, ct);

		logger.LogInformation(
			"Job {JobId} matched {MatchCount} subscription(s)",
			jobId,
			newlyMatched.Count);
	}

	/// <summary>
	/// Sends each person at most one message for this job, however many of their alerts matched
	/// it, and never one they already received through another alert. The first matching alert
	/// carries the message; the rest are closed out as already sent.
	/// </summary>
	private async Task EnqueueInstantAsync(
		AlertRepo repo,
		IReadOnlyList<AlertSubscriptionEntity> instant,
		JobSnapshot job,
		DateTimeOffset now,
		CancellationToken ct)
	{
		// One lookup for the whole batch rather than one per matched subscription.
		var subscribers = await repo.GetSubscribersAsync([.. instant.Select(s => s.SubscriberId).Distinct()], ct);
		var deliveries = (await repo.GetDeliveriesForJobAsync(job.Id, [.. instant.Select(s => s.Id)], ct))
			.ToDictionary(d => d.SubscriptionId);

		foreach (var group in instant.GroupBy(s => s.SubscriberId))
		{
			var alerts = group.OrderBy(s => s.CreatedAt).ToList();

			if (!subscribers.TryGetValue(group.Key, out var subscriber)
				|| subscriber.Status != AlertSubscriberStatus.Confirmed)
			{
				continue;
			}

			// A message that failed or was cancelled earlier still owns the idempotency key, so it
			// also counts: re-adding it would violate the unique index and fail the whole batch.
			var alreadySent = await repo.GetJobsAlreadySentAsync(subscriber.Id, [job.Id], ct);
			if (alreadySent.Contains(job.Id)
				|| await repo.MessageExistsAsync(InstantKey(subscriber.Id, job.Id), ct))
			{
				foreach (var alert in alerts)
					SkipAsAlreadySent(deliveries, alert.Id);

				continue;
			}

			var carrier = alerts[0];
			var message = BuildInstantMessage(carrier, subscriber, job, now);
			repo.AddMessage(message);

			if (deliveries.TryGetValue(carrier.Id, out var carried))
			{
				carried.Message = message;
				carried.Status = AlertDeliveryStatus.Queued;
			}

			foreach (var other in alerts.Skip(1))
				SkipAsAlreadySent(deliveries, other.Id);
		}
	}

	private static void SkipAsAlreadySent(
		IReadOnlyDictionary<Guid, AlertDeliveryEntity> deliveries,
		Guid subscriptionId)
	{
		if (!deliveries.TryGetValue(subscriptionId, out var delivery))
			return;

		delivery.Status = AlertDeliveryStatus.Skipped;
		delivery.SkipReason = AlertDeliverySkipReason.AlreadySent;
	}

	private AlertMessageEntity BuildInstantMessage(
		AlertSubscriptionEntity subscription,
		AlertSubscriberEntity subscriber,
		JobSnapshot job,
		DateTimeOffset now)
	{
		var manageToken = AlertTokens.DeriveManageToken(subscriber.Id, _options.TokenSecret);

		var rendered = renderers.Get(subscriber.Channel).RenderInstantMatch(new AlertMatchModel(
			subscription.Name,
			ToJobModel(job),
			links.Manage(manageToken),
			links.Unsubscribe(manageToken)));

		return new AlertMessageEntity
		{
			Id = Guid.CreateVersion7(),
			SubscriberId = subscriber.Id,
			SubscriptionId = subscription.Id,
			Kind = AlertMessageKind.InstantMatch,
			Channel = subscriber.Channel,
			Address = subscriber.Address,
			// Keyed per person and job, so the unique index is a database backstop for the
			// never-twice rule even if the checks above were ever bypassed.
			IdempotencyKey = InstantKey(subscriber.Id, job.Id),
			Status = AlertMessageStatus.Pending,
			Subject = rendered.Subject,
			BodyText = rendered.BodyText,
			BodyHtml = rendered.BodyHtml,
			AvailableAt = now,
			CreatedAt = now
		};
	}

	private static string InstantKey(Guid subscriberId, int jobId) => $"instant:{subscriberId}:{jobId}";

	private void MarkProcessed(IReadOnlyList<JobEventEntity> events, DateTimeOffset now)
	{
		foreach (var jobEvent in events)
		{
			jobEvent.ProcessedAt = now;

			// A poison event is retired rather than left to block everything behind it.
			if (jobEvent.LastError is not null && jobEvent.Attempts >= _options.MaxOutboxAttempts)
			{
				logger.LogError(
					"Job event {EventId} for job {JobId} retired after {Attempts} attempts: {Error}",
					jobEvent.Id,
					jobEvent.JobId,
					jobEvent.Attempts,
					jobEvent.LastError);
			}
		}
	}

	private static AlertJobModel ToJobModel(JobSnapshot job) => new(
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
