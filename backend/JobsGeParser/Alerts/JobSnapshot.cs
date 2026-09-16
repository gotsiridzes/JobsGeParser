using JobsGeParser.Data;

namespace JobsGeParser.Alerts;

/// <summary>
/// The minimal job projection the matcher needs. Deliberately not the entity: matching must not
/// depend on lazy navigation or on fields that are recomputed later.
/// </summary>
public sealed record JobSnapshot(
	int Id,
	string Name,
	string Company,
	string Link,
	string? Description,
	string? City,
	string? WorkMode,
	string? EmploymentType,
	string? Seniority,
	string? LanguageRequirement,
	decimal? SalaryMin,
	decimal? SalaryMax,
	string? SalaryCurrency,
	IReadOnlyList<string> CategorySlugs,
	DateTimeOffset FirstSeenAt)
{
	public static JobSnapshot From(JobEntity job, IReadOnlyList<string> categorySlugs) => new(
		job.Id,
		job.Name,
		job.Company,
		job.Link,
		job.Description,
		job.City,
		job.WorkMode,
		job.EmploymentType,
		job.Seniority,
		job.LanguageRequirement,
		job.SalaryMin,
		job.SalaryMax,
		job.SalaryCurrency,
		categorySlugs,
		job.FirstSeenAt);
}
