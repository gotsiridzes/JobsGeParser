namespace JobsGeParser.Alerts.Rendering;

/// <summary>
/// One job as it appears in an alert. Values come from scraped third party HTML, so any renderer
/// that emits markup must escape them.
/// </summary>
public sealed record AlertJobModel(
	int Id,
	string Title,
	string Company,
	string Link,
	string? City,
	string? WorkMode,
	string? Seniority,
	decimal? SalaryMin,
	decimal? SalaryMax,
	string? SalaryCurrency);

public sealed record AlertConfirmationModel(
	string ConfirmUrl,
	string SubscriptionName,
	int CurrentlyMatchingJobs);

public sealed record AlertMatchModel(
	string SubscriptionName,
	AlertJobModel Job,
	string ManageUrl,
	string UnsubscribeUrl);

public sealed record AlertDigestModel(
	string SubscriptionName,
	IReadOnlyList<AlertJobModel> Jobs,
	int TotalMatches,
	string ManageUrl,
	string UnsubscribeUrl);
