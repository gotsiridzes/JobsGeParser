using JobsGeParser.Alerts;

namespace JobsGeParser.Endpoints.Dtos;

public record AlertCriteriaDto(
	string? Query = null,
	string? TitleContains = null,
	string? DescriptionContains = null,
	string? CompanyContains = null,
	IReadOnlyList<string>? CategorySlugs = null,
	IReadOnlyList<string>? Cities = null,
	IReadOnlyList<string>? WorkModes = null,
	IReadOnlyList<string>? EmploymentTypes = null,
	IReadOnlyList<string>? Seniorities = null,
	IReadOnlyList<string>? Languages = null,
	decimal? SalaryMin = null,
	decimal? SalaryMax = null,
	string? SalaryCurrency = null,
	bool RequireSalary = false)
{
	public AlertCriteria ToCriteria() => new(
		Query,
		TitleContains,
		DescriptionContains,
		CompanyContains,
		CategorySlugs,
		Cities,
		WorkModes,
		EmploymentTypes,
		Seniorities,
		Languages,
		SalaryMin,
		SalaryMax,
		SalaryCurrency,
		RequireSalary);

	public static AlertCriteriaDto From(AlertCriteria criteria) => new(
		criteria.Query,
		criteria.TitleContains,
		criteria.DescriptionContains,
		criteria.CompanyContains,
		criteria.CategorySlugs,
		criteria.Cities,
		criteria.WorkModes,
		criteria.EmploymentTypes,
		criteria.Seniorities,
		criteria.Languages,
		criteria.SalaryMin,
		criteria.SalaryMax,
		criteria.SalaryCurrency,
		criteria.RequireSalary);
}

public record AlertPreviewRequest(AlertCriteriaDto Criteria, int? Page = null, int? PageSize = null);

/// <summary>
/// The vocabularies a filter may use, so a client can build the filter UI without hardcoding them.
/// </summary>
public record AlertCriteriaOptionsDto(
	IReadOnlyList<string> CategorySlugs,
	IReadOnlyList<string> Cities,
	IReadOnlyList<string> WorkModes,
	IReadOnlyList<string> EmploymentTypes,
	IReadOnlyList<string> Seniorities,
	IReadOnlyList<string> Languages,
	int MaxTextLength,
	int MaxArrayLength);

public record AlertValidationErrorDto(IReadOnlyList<string> Errors);

public record CreateAlertSubscriptionRequest(
	string Channel,
	string Address,
	string Name,
	string Cadence,
	TimeOnly? DigestSendLocalTime,
	string? TimeZoneId,
	AlertCriteriaDto Criteria);

/// <summary>
/// Deliberately uninformative. The same response comes back whether the address was unknown,
/// already confirmed or suppressed, so this endpoint cannot be used to test whether an address
/// is registered.
/// </summary>
public record CreateAlertSubscriptionResponse(string Status, int CurrentlyMatchingJobs);

public record ConfirmAlertRequest(string Token);

/// <summary>
/// The manage token is returned exactly once, here. It is derived rather than stored, so it can
/// be rebuilt for message footers without keeping a usable token in the database.
/// </summary>
public record ConfirmAlertResponse(string ManageToken, string ManageUrl, int ActivatedSubscriptions);

public record UpdateAlertSubscriptionRequest(
	string? Name = null,
	string? Cadence = null,
	TimeOnly? DigestSendLocalTime = null,
	AlertCriteriaDto? Criteria = null);

public record UnsubscribeAlertRequest(string Token, Guid? SubscriptionId = null);

public record AlertSubscriptionDto(
	Guid Id,
	string Name,
	string Status,
	string Cadence,
	TimeOnly DigestSendLocalTime,
	string Channel,
	string MaskedAddress,
	DateTimeOffset CreatedAt,
	DateTimeOffset MatchFromAt,
	DateTimeOffset? LastAlertSentAt,
	int AlertsSent,
	AlertCriteriaDto Criteria);

public record AlertSubscriptionsResponse(
	string Channel,
	string MaskedAddress,
	string SubscriberStatus,
	IReadOnlyList<AlertSubscriptionDto> Subscriptions);
