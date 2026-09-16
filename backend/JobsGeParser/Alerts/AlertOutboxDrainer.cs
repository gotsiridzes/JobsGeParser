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
		{
			// One lookup for the whole batch rather than one per matched subscription.
			var subscribers = await repo.GetSubscribersAsync([.. instant.Select(s => s.SubscriberId)], ct);

			foreach (var subscription in instant)
			{
				if (subscribers.TryGetValue(subscription.SubscriberId, out var subscriber))
					EnqueueInstant(repo, subscription, subscriber, job, now);
			}
		}

		logger.LogInformation(
			"Job {JobId} matched {MatchCount} subscription(s)",
			jobId,
			newlyMatched.Count);
	}

	private void EnqueueInstant(
		AlertRepo repo,
		AlertSubscriptionEntity subscription,
		AlertSubscriberEntity subscriber,
		JobSnapshot job,
		DateTimeOffset now)
	{
		if (subscriber.Status != AlertSubscriberStatus.Confirmed)
			return;

		var manageToken = AlertTokens.DeriveManageToken(subscriber.Id, _options.TokenSecret);

		var rendered = renderers.Get(subscriber.Channel).RenderInstantMatch(new AlertMatchModel(
			subscription.Name,
			ToJobModel(job),
			links.Manage(manageToken),
			links.Unsubscribe(manageToken)));

		repo.AddMessage(new AlertMessageEntity
		{
			Id = Guid.CreateVersion7(),
			SubscriberId = subscriber.Id,
			SubscriptionId = subscription.Id,
			Kind = AlertMessageKind.InstantMatch,
			Channel = subscriber.Channel,
			Address = subscriber.Address,
			// One message per subscription and job, matching the ledger's own uniqueness.
			IdempotencyKey = $"instant:{subscription.Id}:{job.Id}",
			Status = AlertMessageStatus.Pending,
			Subject = rendered.Subject,
			BodyText = rendered.BodyText,
			BodyHtml = rendered.BodyHtml,
			AvailableAt = now,
			CreatedAt = now
		});
	}

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
