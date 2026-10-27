namespace JobsGeParser.Data;

/// <summary>
/// One saved filter plus its cadence. Filter and subscription are deliberately one table:
/// nothing needs to share a filter across subscriptions, and the reverse match is the hottest
/// query in the subsystem. Reuse comes from the AlertCriteria record, not from a shared row.
/// </summary>
public class AlertSubscriptionEntity
{
	public Guid Id { get; set; }

	public Guid SubscriberId { get; set; }

	public AlertSubscriberEntity? Subscriber { get; set; }

	public string Name { get; set; } = null!;

	public string Status { get; set; } = null!;

	public AlertCadence Cadence { get; set; }

	/// <summary>
	/// Wall clock time in the subscriber's timezone at which the daily digest is due.
	/// </summary>
	public TimeOnly DigestSendLocalTime { get; set; }

	/// <summary>
	/// Local calendar date of the last digest. Together with the message idempotency key this
	/// makes a double digest impossible, including across a DST transition.
	/// </summary>
	public DateOnly? LastDigestSentOn { get; set; }

	/// <summary>
	/// Only jobs first seen at or after this instant may match. Stops a new subscription from
	/// alerting on the entire existing corpus.
	/// </summary>
	public DateTimeOffset MatchFromAt { get; set; }

	/// <summary>
	/// SHA-256 over the canonicalized criteria, so the same filter cannot be saved twice.
	/// </summary>
	public byte[] CriteriaHash { get; set; } = null!;

	public DateTimeOffset CreatedAt { get; set; }

	public DateTimeOffset UpdatedAt { get; set; }

	/// <summary>
	/// Free text query, evaluated with websearch_to_tsquery('simple', ...) against SearchVector,
	/// matching the semantics of the public search endpoint exactly.
	/// </summary>
	public string? Query { get; set; }

	public string? TitleContains { get; set; }

	public string? DescriptionContains { get; set; }

	public string? CompanyContains { get; set; }

	/// <summary>
	/// Null or empty means "any". Same for every array below.
	/// </summary>
	public string[]? CategorySlugs { get; set; }

	public string[]? Cities { get; set; }

	public string[]? WorkModes { get; set; }

	public string[]? EmploymentTypes { get; set; }

	public string[]? Seniorities { get; set; }

	public string[]? Languages { get; set; }

	public decimal? SalaryMin { get; set; }

	public decimal? SalaryMax { get; set; }

	public string? SalaryCurrency { get; set; }

	/// <summary>
	/// When false a salary bound narrows only jobs that actually have salary data. Salary is
	/// sparsely extracted, so requiring it by default would silently exclude most jobs.
	/// </summary>
	public bool RequireSalary { get; set; }
}
