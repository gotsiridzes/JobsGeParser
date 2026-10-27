using System.Linq.Expressions;
using JobsGeParser.Endpoints.Dtos;

namespace JobsGeParser.Data;

/// <summary>
/// Shared EF projections, so the list card shape is defined once and every endpoint that returns
/// job cards stays in step.
/// </summary>
public static class JobProjections
{
	public static readonly Expression<Func<JobEntity, JobListItemDto>> ToListItem = j => new JobListItemDto(
		j.Id,
		j.Name,
		j.Link,
		j.Company,
		j.CompanyLink,
		j.Published,
		j.EndDate,
		j.LastSeenAt,
		j.SalaryMin,
		j.SalaryMax,
		j.SalaryCurrency,
		j.SalaryPeriod,
		j.City,
		j.WorkMode,
		j.EmploymentType,
		j.Seniority,
		j.LanguageRequirement);
}
