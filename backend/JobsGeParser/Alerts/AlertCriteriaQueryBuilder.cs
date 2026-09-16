using JobsGeParser.Data;
using Microsoft.EntityFrameworkCore;

namespace JobsGeParser.Alerts;

/// <summary>
/// Composes a filter as a query over jobs, the forward direction. Used by the preview endpoint,
/// where the question is "which jobs match this filter". The alert pipeline itself runs the
/// reverse direction and does not use this.
/// </summary>
public static class AlertCriteriaQueryBuilder
{
	private const string LikeEscape = "\\";

	public static IQueryable<JobEntity> Apply(IQueryable<JobEntity> jobs, AlertCriteria criteria)
	{
		criteria = criteria.Canonicalize();

		jobs = ApplyFullText(jobs, criteria);
		jobs = ApplySubstrings(jobs, criteria);
		jobs = ApplyFacets(jobs, criteria);
		return ApplySalary(jobs, criteria);
	}

	private static IQueryable<JobEntity> ApplyFullText(IQueryable<JobEntity> jobs, AlertCriteria criteria)
	{
		if (criteria.Query is not { } query)
			return jobs;

		// WebSearchToTsQuery must stay inside the expression tree; a local assignment forces
		// client evaluation and throws at runtime. Same 'simple' config as /api/search, so a
		// filter matches here exactly what it matches there.
		return jobs.Where(j => j.SearchVector.Matches(EF.Functions.WebSearchToTsQuery("simple", query)));
	}

	private static IQueryable<JobEntity> ApplySubstrings(IQueryable<JobEntity> jobs, AlertCriteria criteria)
	{
		if (criteria.TitleContains is { } title)
		{
			var pattern = ToLikePattern(title);
			jobs = jobs.Where(j => EF.Functions.ILike(j.Name, pattern, LikeEscape));
		}

		if (criteria.CompanyContains is { } company)
		{
			var pattern = ToLikePattern(company);
			jobs = jobs.Where(j => EF.Functions.ILike(j.Company, pattern, LikeEscape));
		}

		if (criteria.DescriptionContains is { } description)
		{
			var pattern = ToLikePattern(description);
			jobs = jobs.Where(j => j.Description != null && EF.Functions.ILike(j.Description, pattern, LikeEscape));
		}

		return jobs;
	}

	private static IQueryable<JobEntity> ApplyFacets(IQueryable<JobEntity> jobs, AlertCriteria criteria)
	{
		if (criteria.CategorySlugs is { } slugs)
			jobs = jobs.Where(j => j.JobCategories.Any(jc => slugs.Contains(jc.CategorySlug)));

		if (criteria.Cities is { } cities)
			jobs = jobs.Where(j => j.City != null && cities.Contains(j.City));

		if (criteria.WorkModes is { } workModes)
			jobs = jobs.Where(j => j.WorkMode != null && workModes.Contains(j.WorkMode));

		if (criteria.EmploymentTypes is { } employmentTypes)
			jobs = jobs.Where(j => j.EmploymentType != null && employmentTypes.Contains(j.EmploymentType));

		if (criteria.Seniorities is { } seniorities)
			jobs = jobs.Where(j => j.Seniority != null && seniorities.Contains(j.Seniority));

		if (criteria.Languages is { } languages)
			jobs = jobs.Where(j => j.LanguageRequirement != null && languages.Contains(j.LanguageRequirement));

		return jobs;
	}

	private static IQueryable<JobEntity> ApplySalary(IQueryable<JobEntity> jobs, AlertCriteria criteria)
	{
		var hasBound = criteria.SalaryMin is not null || criteria.SalaryMax is not null;
		if (!hasBound && !criteria.RequireSalary)
			return jobs;

		if (criteria.RequireSalary)
			jobs = jobs.Where(j => j.SalaryMin != null || j.SalaryMax != null);

		if (criteria.SalaryCurrency is { } currency)
		{
			jobs = jobs.Where(j =>
				j.SalaryMin == null && j.SalaryMax == null
				|| j.SalaryCurrency != null && j.SalaryCurrency.ToLower() == currency.ToLower());
		}

		// Mirrors AlertCriteriaMatcher.MatchesSalary: a job with no salary data is only excluded
		// when the user explicitly required a stated salary.
		if (criteria.SalaryMin is { } wanted)
		{
			jobs = jobs.Where(j =>
				j.SalaryMin == null && j.SalaryMax == null
				|| (j.SalaryMax ?? j.SalaryMin) >= wanted);
		}

		if (criteria.SalaryMax is { } ceiling)
		{
			jobs = jobs.Where(j =>
				j.SalaryMin == null && j.SalaryMax == null
				|| (j.SalaryMin ?? j.SalaryMax) <= ceiling);
		}

		return jobs;
	}

	/// <summary>
	/// Escapes the LIKE wildcards so a user typing '%' searches for a percent sign rather than
	/// widening their own filter to everything.
	/// </summary>
	private static string ToLikePattern(string value)
	{
		var escaped = value
			.Replace("\\", "\\\\")
			.Replace("%", "\\%")
			.Replace("_", "\\_");

		return $"%{escaped}%";
	}
}
