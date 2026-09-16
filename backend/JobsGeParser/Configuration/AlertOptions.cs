namespace JobsGeParser.Configuration;

public class AlertOptions
{
	/// <summary>
	/// When false the scrape path writes no outbox rows and no alert worker starts.
	/// </summary>
	public bool Enabled { get; set; }

	/// <summary>
	/// Base URL used to build confirm, manage and unsubscribe links.
	/// </summary>
	public string PublicBaseUrl { get; set; } = "http://localhost:5173";

	/// <summary>
	/// IANA timezone assigned to a subscriber who does not supply one.
	/// </summary>
	public string DefaultTimeZoneId { get; set; } = "Asia/Tbilisi";

	/// <summary>
	/// Secret used to derive manage and unsubscribe tokens. Never in appsettings: set it with
	/// user secrets or the Alerts__TokenSecret environment variable. Changing it invalidates
	/// every existing manage and unsubscribe link.
	/// </summary>
	public string TokenSecret { get; set; } = "";

	/// <summary>
	/// Delay before a job event becomes claimable. Lets one scrape tick settle so parallel
	/// category passes finish linking before matching runs.
	/// </summary>
	public int EventSettleDelaySeconds { get; set; } = 60;

	public int OutboxDrainIntervalSeconds { get; set; } = 30;

	public int OutboxBatchSize { get; set; } = 100;

	/// <summary>
	/// A claimed but unprocessed event older than this is released back to the queue.
	/// </summary>
	public int OutboxClaimTimeoutMinutes { get; set; } = 10;

	public int MaxOutboxAttempts { get; set; } = 5;

	public int SendIntervalSeconds { get; set; } = 10;

	public int SendBatchSize { get; set; } = 50;

	public int MaxSendAttempts { get; set; } = 6;

	public int RetryBaseDelaySeconds { get; set; } = 30;

	public int RetryMaxDelayMinutes { get; set; } = 30;

	public int DigestScanIntervalMinutes { get; set; } = 5;

	public int ConfirmationTokenTtlHours { get; set; } = 48;

	public int MaxSubscriptionsPerSubscriber { get; set; } = 10;

	public int MaxConfirmationsPerAddressPerDay { get; set; } = 3;

	/// <summary>
	/// Circuit breaker on the whole subscribe endpoint, bounding worst case provider cost.
	/// </summary>
	public int MaxConfirmationsPerDay { get; set; } = 500;

	public int MaxConsecutiveFailures { get; set; } = 3;

	/// <summary>
	/// Consecutive transient failures on one channel before it is paused.
	/// </summary>
	public int ChannelFailureThreshold { get; set; } = 5;

	/// <summary>
	/// How long a channel stays paused. Messages skipped during a cooldown do not consume a send
	/// attempt, so a long provider outage cannot dead letter the whole backlog.
	/// </summary>
	public int ChannelCooldownSeconds { get; set; } = 120;

	public int MessageRetentionDays { get; set; } = 90;

	public AlertEmailOptions Email { get; set; } = new();

	public AlertSmsOptions Sms { get; set; } = new();
}
