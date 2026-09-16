using JobsGeParser.Alerts.Channels;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Alerts;

/// <summary>
/// Drains the outbound message queue. A singleton so the worker stays a thin timer and this can
/// be driven directly by a test, the same split as JobScrapeWorker and ScrapeBatchRunner.
/// </summary>
public sealed class AlertSendPump(
	IServiceScopeFactory scopeFactory,
	AlertChannelRegistry channels,
	AlertSendThrottle throttle,
	AlertLinkBuilder links,
	IOptions<AlertOptions> options,
	TimeProvider timeProvider,
	ILogger<AlertSendPump> logger)
{
	private readonly AlertOptions _options = options.Value;

	/// <summary>
	/// Claims a batch and sends it. Returns how many messages were attempted.
	/// </summary>
	public async Task<int> PumpOnceAsync(CancellationToken ct = default)
	{
		using var scope = scopeFactory.CreateScope();
		var repo = scope.ServiceProvider.GetRequiredService<AlertRepo>();

		await repo.RecoverStaleClaimsAsync(ct);

		var claimed = await repo.ClaimMessagesAsync(_options.SendBatchSize, ct);
		if (claimed.Count == 0)
			return 0;

		var attempted = 0;

		foreach (var message in claimed)
		{
			if (ct.IsCancellationRequested)
				break;

			if (await TrySendAsync(repo, message, ct))
				attempted++;
		}

		await repo.SaveChangesAsync(ct);
		return attempted;
	}

	private async Task<bool> TrySendAsync(AlertRepo repo, AlertMessageEntity message, CancellationToken ct)
	{
		var now = timeProvider.GetUtcNow();

		// A channel in cooldown is skipped without consuming an attempt, so a long provider
		// outage cannot dead letter the entire backlog.
		if (throttle.IsCoolingDown(message.Channel, out var until))
		{
			message.Status = AlertMessageStatus.Pending;
			message.ClaimedAt = null;
			message.Attempts = Math.Max(message.Attempts - 1, 0);
			message.AvailableAt = until;
			return false;
		}

		// Re-checked immediately before the provider call: the subscriber may have unsubscribed
		// between enqueue and now.
		if (message.Subscriber is { } subscriber && subscriber.Status != AlertSubscriberStatus.Confirmed)
		{
			message.Status = AlertMessageStatus.Cancelled;
			message.LastError = $"Subscriber status is {subscriber.Status}.";
			return false;
		}

		var outbound = ToOutbound(message);

		AlertSendResult result;
		try
		{
			result = await throttle.ExecuteAsync(
				message.Channel,
				() => channels.Get(message.Channel).SendAsync(outbound, ct),
				ct);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			// Leave it Claimed; the stale claim reaper decides its fate per channel policy.
			throw;
		}
		catch (Exception ex)
		{
			result = AlertSendResult.Transient(ex.Message);
		}

		await ApplyResultAsync(repo, message, result, now, ct);
		return true;
	}

	private async Task ApplyResultAsync(
		AlertRepo repo,
		AlertMessageEntity message,
		AlertSendResult result,
		DateTimeOffset now,
		CancellationToken ct)
	{
		switch (result.Outcome)
		{
			case AlertSendOutcome.Sent:
				throttle.RecordSuccess(message.Channel);
				message.Status = AlertMessageStatus.Sent;
				message.SentAt = now;
				message.ProviderMessageId = result.ProviderMessageId;
				message.LastError = null;

				if (message.Subscriber is { } sent)
					sent.ConsecutiveFailures = 0;

				break;

			case AlertSendOutcome.TransientFailure:
				message.LastError = result.Error;

				if (throttle.RecordTransientFailure(message.Channel))
				{
					logger.LogWarning(
						"Channel {Channel} paused for {Cooldown}s after repeated transient failures",
						message.Channel,
						_options.ChannelCooldownSeconds);
				}

				if (message.Attempts >= _options.MaxSendAttempts)
				{
					await DeadLetterAsync(repo, message, "Exhausted send attempts.", ct);
					break;
				}

				message.Status = AlertMessageStatus.Pending;
				message.ClaimedAt = null;
				message.AvailableAt = now + AlertRetrySchedule.NextDelayWithRandomJitter(
					message.Attempts,
					TimeSpan.FromSeconds(_options.RetryBaseDelaySeconds),
					TimeSpan.FromMinutes(_options.RetryMaxDelayMinutes),
					result.RetryAfter);
				break;

			case AlertSendOutcome.PermanentFailure:
			default:
				if (result.SuppressSubscriber)
					await SuppressAsync(repo, message, result.Error, ct);

				await DeadLetterAsync(repo, message, result.Error ?? "Permanent send failure.", ct);
				break;
		}
	}

	private async Task DeadLetterAsync(
		AlertRepo repo,
		AlertMessageEntity message,
		string error,
		CancellationToken ct)
	{
		message.Status = AlertMessageStatus.DeadLettered;
		message.LastError = error;

		logger.LogError(
			"Alert message {MessageId} to subscriber {SubscriberId} dead lettered after {Attempts} attempts: {Error}",
			message.Id,
			message.SubscriberId,
			message.Attempts,
			error);

		if (message.Subscriber is not { } subscriber)
			return;

		subscriber.ConsecutiveFailures++;
		if (subscriber.ConsecutiveFailures >= _options.MaxConsecutiveFailures)
			await SuppressAsync(repo, message, $"{subscriber.ConsecutiveFailures} consecutive delivery failures.", ct);
	}

	private async Task SuppressAsync(
		AlertRepo repo,
		AlertMessageEntity message,
		string? reason,
		CancellationToken ct)
	{
		if (message.Subscriber is not { } subscriber || subscriber.Status == AlertSubscriberStatus.Suppressed)
			return;

		var now = timeProvider.GetUtcNow();
		subscriber.Status = AlertSubscriberStatus.Suppressed;
		subscriber.SuppressedAt = now;
		subscriber.SuppressionReason = Truncate(reason ?? "Delivery failed permanently.", 200);
		subscriber.UpdatedAt = now;

		foreach (var subscription in subscriber.Subscriptions)
		{
			if (subscription.Status == AlertSubscriptionStatus.Active)
			{
				subscription.Status = AlertSubscriptionStatus.Paused;
				subscription.UpdatedAt = now;
			}
		}

		logger.LogWarning("Subscriber {SubscriberId} suppressed: {Reason}", subscriber.Id, subscriber.SuppressionReason);

		// Nothing else queued for this subscriber should go out. The message currently in hand
		// keeps its own outcome, which the caller writes straight after this.
		await repo.CancelQueuedMessagesAsync(subscriber.Id, ct);
	}

	private OutboundAlertMessage ToOutbound(AlertMessageEntity message)
	{
		var unsubscribeUrl = links.OneClickUnsubscribe(
			AlertTokens.DeriveManageToken(message.SubscriberId, _options.TokenSecret));

		return new OutboundAlertMessage(
			message.Id,
			message.Address,
			message.Subject,
			message.BodyText,
			message.BodyHtml,
			message.IdempotencyKey,
			unsubscribeUrl);
	}

	private static string Truncate(string value, int maxLength) =>
		value.Length <= maxLength ? value : value[..maxLength];
}
