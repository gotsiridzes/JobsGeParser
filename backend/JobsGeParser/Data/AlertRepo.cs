using System.Data.Common;
using JobsGeParser.Alerts;
using JobsGeParser.Configuration;
using JobsGeParser.Endpoints.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;

namespace JobsGeParser.Data;

/// <summary>
/// All alert reads and writes. Kept separate from <see cref="Repo"/> so the scrape path and the
/// alert path do not grow into one another, but registered and injected the same way.
/// </summary>
public class AlertRepo(
	JobsDbContext db,
	IOptions<JobsGeParserOptions> options,
	IOptions<AlertOptions> alertOptions,
	TimeProvider timeProvider)
{
	private readonly JobsDbContext _db = db;
	private readonly JobsGeParserOptions _options = options.Value;
	private readonly AlertOptions _alertOptions = alertOptions.Value;

	public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

	// Preview and vocabulary

	/// <summary>
	/// Runs a filter forward against the current corpus. Used to show what a filter would have
	/// caught before anyone subscribes to it, and to set expectations at subscribe time.
	/// </summary>
	public async Task<JobsPageDto> PreviewAsync(
		AlertCriteria criteria,
		int page,
		int pageSize,
		CancellationToken ct = default)
	{
		pageSize = Math.Clamp(pageSize, 1, _options.MaxJobsPageSize);
		page = Math.Max(page, 1);

		var matched = AlertCriteriaQueryBuilder.Apply(_db.Jobs.AsNoTracking(), criteria);

		var totalCount = await matched.CountAsync(ct);

		var items = await matched
			.OrderByDescending(j => j.LastSeenAt)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(JobProjections.ToListItem)
			.ToListAsync(ct);

		var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

		return new JobsPageDto(items, totalCount, page, pageSize, totalPages);
	}

	public Task<int> CountMatchingAsync(AlertCriteria criteria, CancellationToken ct = default) =>
		AlertCriteriaQueryBuilder.Apply(_db.Jobs.AsNoTracking(), criteria).CountAsync(ct);

	public async Task<IReadOnlySet<string>> GetKnownCategorySlugsAsync(CancellationToken ct = default)
	{
		var slugs = await _db.Categories
			.AsNoTracking()
			.Select(c => c.Slug)
			.ToListAsync(ct);

		return slugs.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	// Subscribers

	public Task<AlertSubscriberEntity?> FindSubscriberAsync(
		AlertChannelKind channel,
		string address,
		CancellationToken ct = default) =>
		_db.AlertSubscribers.FirstOrDefaultAsync(s => s.Channel == channel && s.Address == address, ct);

	public Task<AlertSubscriberEntity?> FindByManageTokenAsync(byte[] tokenHash, CancellationToken ct = default) =>
		_db.AlertSubscribers
			.Include(s => s.Subscriptions)
			.FirstOrDefaultAsync(s => s.ManageTokenHash == tokenHash, ct);

	public Task<AlertSubscriberEntity?> FindByConfirmationTokenAsync(byte[] tokenHash, CancellationToken ct = default) =>
		_db.AlertSubscribers
			.Include(s => s.Subscriptions)
			.FirstOrDefaultAsync(s => s.ConfirmationTokenHash == tokenHash, ct);

	/// <summary>
	/// Loads several subscribers at once, so a batch of matches costs one query rather than one
	/// per match.
	/// </summary>
	public async Task<IReadOnlyDictionary<Guid, AlertSubscriberEntity>> GetSubscribersAsync(
		IReadOnlyList<Guid> ids,
		CancellationToken ct = default)
	{
		if (ids.Count == 0)
			return new Dictionary<Guid, AlertSubscriberEntity>();

		var subscribers = await _db.AlertSubscribers
			.Where(s => ids.Contains(s.Id))
			.ToListAsync(ct);

		return subscribers.ToDictionary(s => s.Id);
	}

	public AlertSubscriberEntity AddSubscriber(AlertSubscriberEntity subscriber)
	{
		_db.AlertSubscribers.Add(subscriber);
		return subscriber;
	}

	/// <summary>
	/// Global confirmation cap, a circuit breaker on the one endpoint that sends to an arbitrary
	/// caller-supplied address.
	/// </summary>
	public Task<int> CountConfirmationsSinceAsync(DateTimeOffset since, CancellationToken ct = default) =>
		_db.AlertMessages.CountAsync(
			m => m.Kind == AlertMessageKind.Confirmation && m.CreatedAt >= since,
			ct);

	public Task<int> CountActiveSubscriptionsAsync(Guid subscriberId, CancellationToken ct = default) =>
		_db.AlertSubscriptions.CountAsync(
			s => s.SubscriberId == subscriberId && s.Status != AlertSubscriptionStatus.Deleted,
			ct);

	// Subscriptions

	public AlertSubscriptionEntity AddSubscription(AlertSubscriptionEntity subscription)
	{
		_db.AlertSubscriptions.Add(subscription);
		return subscription;
	}

	public Task<AlertSubscriptionEntity?> FindSubscriptionAsync(
		Guid subscriberId,
		Guid subscriptionId,
		CancellationToken ct = default) =>
		_db.AlertSubscriptions.FirstOrDefaultAsync(
			s => s.Id == subscriptionId
				&& s.SubscriberId == subscriberId
				&& s.Status != AlertSubscriptionStatus.Deleted,
			ct);

	/// <param name="excludeId">
	/// The subscription being edited, so saving an unchanged filter does not collide with itself.
	/// </param>
	public Task<AlertSubscriptionEntity?> FindDuplicateSubscriptionAsync(
		Guid subscriberId,
		byte[] criteriaHash,
		CancellationToken ct = default,
		Guid? excludeId = null) =>
		_db.AlertSubscriptions.FirstOrDefaultAsync(
			s => s.SubscriberId == subscriberId
				&& s.CriteriaHash == criteriaHash
				&& s.Status != AlertSubscriptionStatus.Deleted
				&& (excludeId == null || s.Id != excludeId),
			ct);

	public async Task<IReadOnlyList<AlertSubscriptionEntity>> GetSubscriptionsAsync(
		Guid subscriberId,
		CancellationToken ct = default) =>
		await _db.AlertSubscriptions
			.Where(s => s.SubscriberId == subscriberId && s.Status != AlertSubscriptionStatus.Deleted)
			.OrderBy(s => s.CreatedAt)
			.ToListAsync(ct);

	/// <summary>
	/// Per-subscription send counters, read in one grouped query rather than per row.
	/// </summary>
	public async Task<IReadOnlyDictionary<Guid, (int Sent, DateTimeOffset? LastSentAt)>> GetSubscriptionStatsAsync(
		Guid subscriberId,
		CancellationToken ct = default)
	{
		var rows = await _db.AlertDeliveries
			.AsNoTracking()
			.Where(d => d.Subscription!.SubscriberId == subscriberId && d.Status == AlertDeliveryStatus.Sent)
			.GroupBy(d => d.SubscriptionId)
			.Select(g => new { SubscriptionId = g.Key, Sent = g.Count(), LastSentAt = g.Max(d => d.MatchedAt) })
			.ToListAsync(ct);

		return rows.ToDictionary(r => r.SubscriptionId, r => (r.Sent, (DateTimeOffset?)r.LastSentAt));
	}

	// Messages

	public AlertMessageEntity AddMessage(AlertMessageEntity message)
	{
		_db.AlertMessages.Add(message);
		return message;
	}

	public Task<bool> MessageExistsAsync(string idempotencyKey, CancellationToken ct = default) =>
		_db.AlertMessages.AnyAsync(m => m.IdempotencyKey == idempotencyKey, ct);

	/// <summary>
	/// Leases a batch of sendable messages. SKIP LOCKED lets several pumps run without ever
	/// handing the same message to two senders. Attempts is incremented here, before any provider
	/// call, so a crash mid-send cannot produce an unbounded retry loop.
	/// </summary>
	public async Task<IReadOnlyList<AlertMessageEntity>> ClaimMessagesAsync(
		int batchSize,
		CancellationToken ct = default)
	{
		var ids = await ExecuteClaimAsync(batchSize, ct);
		if (ids.Count == 0)
			return [];

		var messages = await _db.AlertMessages
			.Include(m => m.Subscriber)
			.Where(m => ids.Contains(m.Id))
			.ToListAsync(ct);

		// Preserve the claim order so the oldest message is sent first.
		var order = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
		return [.. messages.OrderBy(m => order[m.Id])];
	}

	private async Task<IReadOnlyList<Guid>> ExecuteClaimAsync(int batchSize, CancellationToken ct)
	{
		var connection = _db.Database.GetDbConnection();
		var opened = connection.State != System.Data.ConnectionState.Open;

		if (opened)
			await connection.OpenAsync(ct);

		try
		{
			await using var command = connection.CreateCommand();
			// UPDATE ... RETURNING cannot be composed by EF, so this goes through raw ADO.
			command.CommandText = """
				UPDATE alert_messages m
				SET "Status" = 'Claimed', "ClaimedAt" = now(), "Attempts" = m."Attempts" + 1
				FROM (
					SELECT "Id" FROM alert_messages
					WHERE "Status" = 'Pending' AND "AvailableAt" <= now()
					ORDER BY "AvailableAt", "Id"
					LIMIT @batchSize
					FOR UPDATE SKIP LOCKED
				) c
				WHERE m."Id" = c."Id"
				RETURNING m."Id";
				""";

			AddParameter(command, "batchSize", batchSize);

			var ids = new List<Guid>(batchSize);
			await using var reader = await command.ExecuteReaderAsync(ct);
			while (await reader.ReadAsync(ct))
				ids.Add(reader.GetGuid(0));

			return ids;
		}
		finally
		{
			if (opened)
				await connection.CloseAsync();
		}
	}

	/// <summary>
	/// Returns messages stuck in Claimed by a crashed pump. Email prefers a possible duplicate
	/// over a lost alert; SMS prefers a lost alert over a duplicate that costs money.
	/// </summary>
	public async Task<int> RecoverStaleClaimsAsync(CancellationToken ct = default)
	{
		var cutoff = timeProvider.GetUtcNow() - TimeSpan.FromMinutes(_alertOptions.OutboxClaimTimeoutMinutes);

		var stale = await _db.AlertMessages
			.Where(m => m.Status == AlertMessageStatus.Claimed && m.ClaimedAt != null && m.ClaimedAt < cutoff)
			.ToListAsync(ct);

		if (stale.Count == 0)
			return 0;

		foreach (var message in stale)
		{
			var retry = message.Channel == AlertChannelKind.Email
				? _alertOptions.Email.RetryAfterClaimTimeout
				: _alertOptions.Sms.RetryAfterClaimTimeout;

			if (retry && message.Attempts < _alertOptions.MaxSendAttempts)
			{
				message.Status = AlertMessageStatus.Pending;
				message.ClaimedAt = null;
				message.AvailableAt = timeProvider.GetUtcNow();
			}
			else
			{
				message.Status = AlertMessageStatus.DeadLettered;
				message.LastError = "Claim timed out and this channel does not retry after a timeout.";
			}
		}

		await _db.SaveChangesAsync(ct);
		return stale.Count;
	}

	public Task<AlertMessageEntity?> FindMessageAsync(Guid messageId, CancellationToken ct = default) =>
		_db.AlertMessages
			.Include(m => m.Subscriber)
			.FirstOrDefaultAsync(m => m.Id == messageId, ct);

	/// <summary>
	/// Cancels everything still queued for a subscriber, used when they unsubscribe or are
	/// suppressed. Anything already handed to the provider is beyond recall.
	/// </summary>
	public Task<int> CancelQueuedMessagesAsync(Guid subscriberId, CancellationToken ct = default) =>
		_db.AlertMessages
			.Where(m => m.SubscriberId == subscriberId
				&& (m.Status == AlertMessageStatus.Pending || m.Status == AlertMessageStatus.Claimed))
			.ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, AlertMessageStatus.Cancelled), ct);

	// Outbox and matching

	public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
		_db.Database.BeginTransactionAsync(ct);

	/// <summary>
	/// Releases events leased by a pass that never finished, so a crashed drain does not park
	/// work forever. Runs before each claim.
	/// </summary>
	public async Task<int> ReleaseStaleEventClaimsAsync(CancellationToken ct = default)
	{
		var cutoff = timeProvider.GetUtcNow() - TimeSpan.FromMinutes(_alertOptions.OutboxClaimTimeoutMinutes);

		return await _db.JobEvents
			.Where(e => e.ProcessedAt == null && e.ClaimedAt != null && e.ClaimedAt < cutoff)
			.ExecuteUpdateAsync(
				s => s.SetProperty(e => e.ClaimedAt, (DateTimeOffset?)null)
					.SetProperty(e => e.Attempts, e => e.Attempts + 1),
				ct);
	}

	/// <summary>
	/// Leases a batch of due events. SKIP LOCKED means several drains can run at once and no
	/// event is ever handed to two of them. Must be called inside a transaction.
	/// </summary>
	public async Task<IReadOnlyList<JobEventEntity>> ClaimJobEventsAsync(
		int batchSize,
		CancellationToken ct = default)
	{
		// No LINQ composition after this call, so EF sends the statement verbatim and the
		// locking clause survives.
		var events = await _db.JobEvents.FromSqlInterpolated($"""
			SELECT * FROM job_events
			WHERE "ProcessedAt" IS NULL AND "AvailableAt" <= now()
			ORDER BY "Id"
			LIMIT {batchSize}
			FOR UPDATE SKIP LOCKED
			""").ToListAsync(ct);

		var now = timeProvider.GetUtcNow();
		foreach (var jobEvent in events)
			jobEvent.ClaimedAt = now;

		return events;
	}

	public async Task<JobSnapshot?> GetJobSnapshotAsync(int jobId, CancellationToken ct = default)
	{
		var job = await _db.Jobs
			.AsNoTracking()
			.Where(j => j.Id == jobId)
			.Select(j => new
			{
				Entity = j,
				Slugs = j.JobCategories.Select(jc => jc.CategorySlug).ToList()
			})
			.FirstOrDefaultAsync(ct);

		return job is null ? null : JobSnapshot.From(job.Entity, job.Slugs);
	}

	/// <summary>
	/// The reverse match prefilter: which active subscriptions could accept this job. Every
	/// faceted criterion is answered by a GIN containment lookup, so this stays indexed as the
	/// subscription count grows. Containment is used rather than "= ANY" because only the
	/// containment form uses the index.
	/// </summary>
	public async Task<IReadOnlyList<AlertSubscriptionEntity>> FindCandidateSubscriptionsAsync(
		JobSnapshot job,
		CancellationToken ct = default)
	{
		var categories = job.CategorySlugs.ToArray();

		return await _db.AlertSubscriptions.FromSqlInterpolated($"""
			SELECT s.*
			FROM alert_subscriptions s
			JOIN alert_subscribers b ON b."Id" = s."SubscriberId"
			WHERE s."Status" = 'Active'
			  AND b."Status" = 'Confirmed'
			  AND s."MatchFromAt" <= {job.FirstSeenAt}
			  AND (s."CategorySlugs" IS NULL OR s."CategorySlugs" && {categories}::text[])
			  AND (s."Cities" IS NULL OR ({job.City}::text IS NOT NULL AND s."Cities" @> ARRAY[{job.City}::text]))
			  AND (s."WorkModes" IS NULL OR ({job.WorkMode}::text IS NOT NULL AND s."WorkModes" @> ARRAY[{job.WorkMode}::text]))
			  AND (s."EmploymentTypes" IS NULL OR ({job.EmploymentType}::text IS NOT NULL AND s."EmploymentTypes" @> ARRAY[{job.EmploymentType}::text]))
			  AND (s."Seniorities" IS NULL OR ({job.Seniority}::text IS NOT NULL AND s."Seniorities" @> ARRAY[{job.Seniority}::text]))
			  AND (s."Languages" IS NULL OR ({job.LanguageRequirement}::text IS NOT NULL AND s."Languages" @> ARRAY[{job.LanguageRequirement}::text]))
			""").AsNoTracking().ToListAsync(ct);
	}

	/// <summary>
	/// Evaluates the free text criterion for many subscriptions against one job in a single round
	/// trip, using the same 'simple' configuration and stored tsvector as the search endpoint, so
	/// a filter matches in alerts exactly what it matches in search.
	/// </summary>
	public async Task<IReadOnlySet<Guid>> FilterByFullTextAsync(
		int jobId,
		IReadOnlyList<Guid> subscriptionIds,
		CancellationToken ct = default)
	{
		if (subscriptionIds.Count == 0)
			return new HashSet<Guid>();

		var ids = subscriptionIds.ToArray();

		var matched = await _db.Database.SqlQuery<Guid>($"""
			SELECT s."Id" AS "Value"
			FROM alert_subscriptions s, jobs j
			WHERE j."Id" = {jobId}
			  AND s."Id" = ANY({ids}::uuid[])
			  AND j."SearchVector" @@ websearch_to_tsquery('simple', s."Query")
			""").ToListAsync(ct);

		return matched.ToHashSet();
	}

	/// <summary>
	/// Records matches and returns only the subscriptions that did not already have this job.
	/// The unique index turns a replay into a no-op, so dedup is enforced by Postgres rather than
	/// by application logic, and the returned set is exactly what still needs alerting.
	/// </summary>
	public async Task<IReadOnlySet<Guid>> InsertDeliveriesAsync(
		int jobId,
		IReadOnlyList<Guid> subscriptionIds,
		CancellationToken ct = default)
	{
		if (subscriptionIds.Count == 0)
			return new HashSet<Guid>();

		var ids = subscriptionIds.ToArray();
		var now = timeProvider.GetUtcNow();

		var inserted = await _db.Database.SqlQuery<Guid>($"""
			INSERT INTO alert_deliveries ("SubscriptionId", "JobId", "MatchedAt", "Status")
			SELECT unnest({ids}::uuid[]), {jobId}, {now}, {AlertDeliveryStatus.Pending}
			ON CONFLICT ("SubscriptionId", "JobId") DO NOTHING
			RETURNING "SubscriptionId" AS "Value"
			""").ToListAsync(ct);

		return inserted.ToHashSet();
	}

	// Digest

	/// <summary>
	/// Active digest subscriptions that have something waiting. Ordering by id keeps repeated
	/// passes deterministic.
	/// </summary>
	public async Task<IReadOnlyList<AlertSubscriptionEntity>> GetDigestCandidatesAsync(
		int limit,
		CancellationToken ct = default) =>
		await _db.AlertSubscriptions
			.Where(s => s.Status == AlertSubscriptionStatus.Active
				&& s.Cadence == AlertCadence.DailyDigest
				&& _db.AlertDeliveries.Any(d =>
					d.SubscriptionId == s.Id
					&& d.MessageId == null
					&& d.Status == AlertDeliveryStatus.Pending))
			.OrderBy(s => s.Id)
			.Take(limit)
			.ToListAsync(ct);

	/// <summary>
	/// The jobs waiting in a subscription's digest, oldest match first.
	/// </summary>
	public async Task<IReadOnlyList<(AlertDeliveryEntity Delivery, JobEntity Job)>> GetPendingDeliveriesAsync(
		Guid subscriptionId,
		CancellationToken ct = default)
	{
		var rows = await _db.AlertDeliveries
			.Where(d => d.SubscriptionId == subscriptionId
				&& d.MessageId == null
				&& d.Status == AlertDeliveryStatus.Pending)
			.OrderBy(d => d.MatchedAt)
			.ThenBy(d => d.Id)
			.Join(_db.Jobs, d => d.JobId, j => j.Id, (d, j) => new { Delivery = d, Job = j })
			.ToListAsync(ct);

		return [.. rows.Select(r => (r.Delivery, r.Job))];
	}

	private static void AddParameter(DbCommand command, string name, object value)
	{
		var parameter = new NpgsqlParameter(name, value);
		command.Parameters.Add(parameter);
	}
}
