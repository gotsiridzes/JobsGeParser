namespace JobsGeParser.Alerts;

/// <summary>
/// Results the endpoints translate into status codes. Subscribe deliberately cannot report
/// whether an address was already known, so it has no "already exists" outcome.
/// </summary>
public enum AlertSubscribeOutcome
{
	/// <summary>
	/// Accepted. A confirmation was sent, or the subscription went straight to active because
	/// the address was already proven. Returned in every non-error case so the endpoint cannot
	/// be used to test whether an address is registered.
	/// </summary>
	Accepted = 0,

	Invalid = 1,

	RateLimited = 2
}

public sealed record AlertSubscribeResult(
	AlertSubscribeOutcome Outcome,
	int CurrentlyMatchingJobs = 0,
	IReadOnlyList<string>? Errors = null);

public enum AlertTokenOutcome
{
	Ok = 0,

	/// <summary>
	/// Unknown, expired or already used. Reported identically so a caller cannot tell them apart.
	/// </summary>
	NotFound = 1
}

public sealed record AlertConfirmResult(
	AlertTokenOutcome Outcome,
	string? ManageToken = null,
	int ActivatedSubscriptions = 0);
