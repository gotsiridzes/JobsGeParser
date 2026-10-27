namespace JobsGeParser.Alerts;

/// <summary>
/// Evaluates a filter against one job, in memory, with no I/O. Everything except the free text
/// Query is decided here. Query is left to SQL on purpose: websearch_to_tsquery semantics cannot
/// be reproduced client side, and alerts must match exactly what the search endpoint matches.
/// </summary>
public static class AlertCriteriaMatcher
{
	/// <summary>
	/// True when the job satisfies every set criterion except Query. An unset criterion never
	/// narrows, so an empty filter matches everything.
	/// </summary>
	public static bool Matches(JobSnapshot job, AlertCriteria criteria) =>
		MatchesText(job, criteria)
		&& MatchesFacets(job, criteria)
		&& MatchesSalary(job, criteria);

	private static bool MatchesText(JobSnapshot job, AlertCriteria criteria) =>
		Contains(job.Name, criteria.TitleContains)
		&& Contains(job.Description, criteria.DescriptionContains)
		&& Contains(job.Company, criteria.CompanyContains);

	private static bool MatchesFacets(JobSnapshot job, AlertCriteria criteria) =>
		MatchesAnyCategory(job.CategorySlugs, criteria.CategorySlugs)
		&& MatchesValue(job.City, criteria.Cities)
		&& MatchesValue(job.WorkMode, criteria.WorkModes)
		&& MatchesValue(job.EmploymentType, criteria.EmploymentTypes)
		&& MatchesValue(job.Seniority, criteria.Seniorities)
		&& MatchesValue(job.LanguageRequirement, criteria.Languages);

	private static bool MatchesSalary(JobSnapshot job, AlertCriteria criteria)
	{
		var hasBound = criteria.SalaryMin is not null || criteria.SalaryMax is not null;
		if (!hasBound && !criteria.RequireSalary)
			return true;

		// Salary is sparsely extracted from this corpus. Unless the user explicitly asked for
		// jobs that state a salary, a bound narrows only the jobs that actually have one,
		// otherwise a plausible filter would silently exclude almost everything.
		var jobHasSalary = job.SalaryMin is not null || job.SalaryMax is not null;
		if (!jobHasSalary)
			return !criteria.RequireSalary;

		if (criteria.SalaryCurrency is not null
			&& !string.Equals(job.SalaryCurrency, criteria.SalaryCurrency, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		// "Pay me at least X" is satisfied by the top of the job's range.
		if (criteria.SalaryMin is { } wanted && (job.SalaryMax ?? job.SalaryMin) < wanted)
			return false;

		// "No more than Y" is judged by the bottom of the job's range.
		if (criteria.SalaryMax is { } ceiling && (job.SalaryMin ?? job.SalaryMax) > ceiling)
			return false;

		return true;
	}

	private static bool Contains(string? haystack, string? needle)
	{
		if (string.IsNullOrWhiteSpace(needle))
			return true;

		return haystack is not null
			&& haystack.Contains(needle.Trim(), StringComparison.OrdinalIgnoreCase);
	}

	private static bool MatchesValue(string? jobValue, IReadOnlyList<string>? accepted)
	{
		if (AlertCriteria.IsUnset(accepted))
			return true;

		// An unenriched field cannot satisfy a facet the user explicitly asked for.
		if (jobValue is null)
			return false;

		foreach (var value in accepted!)
		{
			if (string.Equals(value, jobValue, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	private static bool MatchesAnyCategory(
		IReadOnlyList<string> jobCategories,
		IReadOnlyList<string>? accepted)
	{
		if (AlertCriteria.IsUnset(accepted))
			return true;

		foreach (var slug in jobCategories)
		{
			foreach (var value in accepted!)
			{
				if (string.Equals(value, slug, StringComparison.OrdinalIgnoreCase))
					return true;
			}
		}

		return false;
	}
}
