using JobsGeParser.Enrichment;

namespace JobsGeParser.Alerts;

/// <summary>
/// Validates a filter before it reaches the database. Vocabularies come from
/// <see cref="EnrichmentService"/> so a filter can only ask for values the extractor can produce.
/// </summary>
public sealed class AlertCriteriaValidator
{
	public const int MaxTextLength = 200;
	public const int MaxArrayLength = 20;

	public IReadOnlyList<string> Validate(AlertCriteria criteria, IReadOnlySet<string> knownCategorySlugs)
	{
		var errors = new List<string>();
		var canonical = criteria.Canonicalize();

		if (canonical.IsEmpty)
			errors.Add("At least one filter criterion is required.");

		ValidateText(errors, canonical.Query, nameof(criteria.Query));
		ValidateText(errors, canonical.TitleContains, nameof(criteria.TitleContains));
		ValidateText(errors, canonical.DescriptionContains, nameof(criteria.DescriptionContains));
		ValidateText(errors, canonical.CompanyContains, nameof(criteria.CompanyContains));

		ValidateVocabulary(errors, canonical.CategorySlugs, knownCategorySlugs, nameof(criteria.CategorySlugs));
		ValidateVocabulary(errors, canonical.Cities, EnrichmentService.CityValues, nameof(criteria.Cities));
		ValidateVocabulary(errors, canonical.WorkModes, EnrichmentService.WorkModeValues, nameof(criteria.WorkModes));
		ValidateVocabulary(errors, canonical.EmploymentTypes, EnrichmentService.EmploymentTypeValues, nameof(criteria.EmploymentTypes));
		ValidateVocabulary(errors, canonical.Seniorities, EnrichmentService.SeniorityValues, nameof(criteria.Seniorities));
		ValidateVocabulary(errors, canonical.Languages, EnrichmentService.LanguageValues, nameof(criteria.Languages));

		ValidateSalary(errors, canonical);

		return errors;
	}

	private static void ValidateText(List<string> errors, string? value, string field)
	{
		if (value is not null && value.Length > MaxTextLength)
			errors.Add($"{field} cannot exceed {MaxTextLength} characters.");
	}

	private static void ValidateVocabulary(
		List<string> errors,
		IReadOnlyList<string>? values,
		IReadOnlySet<string> allowed,
		string field)
	{
		if (values is null)
			return;

		if (values.Count > MaxArrayLength)
		{
			errors.Add($"{field} cannot contain more than {MaxArrayLength} values.");
			return;
		}

		foreach (var value in values)
		{
			if (!allowed.Contains(value))
				errors.Add($"{field} contains an unknown value: {value}");
		}
	}

	private static void ValidateSalary(List<string> errors, AlertCriteria criteria)
	{
		if (criteria.SalaryMin is < 0)
			errors.Add("SalaryMin cannot be negative.");

		if (criteria.SalaryMax is < 0)
			errors.Add("SalaryMax cannot be negative.");

		if (criteria.SalaryMin is { } min && criteria.SalaryMax is { } max && min > max)
			errors.Add("SalaryMin cannot exceed SalaryMax.");

		if (criteria.SalaryCurrency is { } currency && currency.Length > 10)
			errors.Add("SalaryCurrency cannot exceed 10 characters.");

		if (criteria.SalaryCurrency is not null && criteria.SalaryMin is null && criteria.SalaryMax is null && !criteria.RequireSalary)
			errors.Add("SalaryCurrency has no effect without a salary bound or RequireSalary.");
	}
}
