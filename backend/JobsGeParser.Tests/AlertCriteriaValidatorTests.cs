using JobsGeParser.Alerts;

namespace JobsGeParser.Tests;

public class AlertCriteriaValidatorTests
{
	private readonly AlertCriteriaValidator _sut = new();

	private static readonly IReadOnlySet<string> KnownSlugs =
		new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "it", "finance-statistics", "sales" };

	private IReadOnlyList<string> Validate(AlertCriteria criteria) => _sut.Validate(criteria, KnownSlugs);

	[Fact]
	public void Validate_EmptyCriteria_IsRejected()
	{
		var errors = Validate(new AlertCriteria());

		Assert.Contains(errors, e => e.Contains("At least one filter criterion"));
	}

	[Fact]
	public void Validate_MinimalValidCriteria_HasNoErrors()
	{
		var errors = Validate(new AlertCriteria(TitleContains: "backend"));

		Assert.Empty(errors);
	}

	[Fact]
	public void Validate_UnknownWorkMode_IsRejected()
	{
		var errors = Validate(new AlertCriteria(WorkModes: ["teleport"]));

		Assert.Contains(errors, e => e.Contains("WorkModes") && e.Contains("teleport"));
	}

	[Fact]
	public void Validate_KnownVocabularyValues_AreAccepted()
	{
		var errors = Validate(new AlertCriteria(
			WorkModes: ["remote", "hybrid"],
			EmploymentTypes: ["full_time"],
			Seniorities: ["senior", "lead"],
			Languages: ["en"],
			Cities: ["Tbilisi"]));

		Assert.Empty(errors);
	}

	[Fact]
	public void Validate_VocabularyMatch_IsCaseInsensitive()
	{
		var errors = Validate(new AlertCriteria(WorkModes: ["REMOTE"]));

		Assert.Empty(errors);
	}

	[Fact]
	public void Validate_UnknownCategorySlug_IsRejected()
	{
		var errors = Validate(new AlertCriteria(CategorySlugs: ["not-a-category"]));

		Assert.Contains(errors, e => e.Contains("CategorySlugs"));
	}

	[Fact]
	public void Validate_OversizedArray_IsRejected()
	{
		var tooMany = Enumerable.Range(0, AlertCriteriaValidator.MaxArrayLength + 1)
			.Select(i => $"city{i}")
			.ToArray();

		var errors = Validate(new AlertCriteria(Cities: tooMany));

		Assert.Contains(errors, e => e.Contains("more than"));
	}

	[Fact]
	public void Validate_OversizedText_IsRejected()
	{
		var errors = Validate(new AlertCriteria(Query: new string('a', AlertCriteriaValidator.MaxTextLength + 1)));

		Assert.Contains(errors, e => e.Contains("Query") && e.Contains("exceed"));
	}

	[Fact]
	public void Validate_SalaryMinAboveMax_IsRejected()
	{
		var errors = Validate(new AlertCriteria(SalaryMin: 5000, SalaryMax: 1000));

		Assert.Contains(errors, e => e.Contains("SalaryMin cannot exceed SalaryMax"));
	}

	[Fact]
	public void Validate_NegativeSalary_IsRejected()
	{
		var errors = Validate(new AlertCriteria(SalaryMin: -1));

		Assert.Contains(errors, e => e.Contains("SalaryMin cannot be negative"));
	}

	[Fact]
	public void Validate_CurrencyWithoutSalaryBound_IsRejected()
	{
		var errors = Validate(new AlertCriteria(TitleContains: "dev", SalaryCurrency: "GEL"));

		Assert.Contains(errors, e => e.Contains("SalaryCurrency has no effect"));
	}

	[Fact]
	public void Validate_BlankArrayEntries_AreDroppedNotRejected()
	{
		var errors = Validate(new AlertCriteria(TitleContains: "dev", WorkModes: ["", "  "]));

		Assert.Empty(errors);
	}
}
