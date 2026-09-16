using JobsGeParser.Alerts;
using JobsGeParser.Data;
using Microsoft.EntityFrameworkCore;

namespace JobsGeParser.Tests;

/// <summary>
/// Proves every criterion translates to SQL. ToQueryString compiles the expression tree through
/// the real Npgsql provider without opening a connection, so an untranslatable predicate fails
/// here rather than at runtime in the drain loop.
/// </summary>
public class AlertCriteriaQueryBuilderTests
{
	private static JobsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<JobsDbContext>()
			.UseNpgsql("Host=localhost;Database=translation_only;Username=none;Password=none")
			.Options;

		return new JobsDbContext(options);
	}

	private static string Translate(AlertCriteria criteria)
	{
		using var db = CreateContext();
		return AlertCriteriaQueryBuilder.Apply(db.Jobs.AsNoTracking(), criteria).ToQueryString();
	}

	[Fact]
	public void Apply_EveryCriterionAtOnce_TranslatesToSql()
	{
		var criteria = new AlertCriteria(
			Query: "backend developer",
			TitleContains: "backend",
			DescriptionContains: "dotnet",
			CompanyContains: "acme",
			CategorySlugs: ["it"],
			Cities: ["Tbilisi"],
			WorkModes: ["remote"],
			EmploymentTypes: ["full_time"],
			Seniorities: ["senior"],
			Languages: ["en"],
			SalaryMin: 2000,
			SalaryMax: 8000,
			SalaryCurrency: "GEL",
			RequireSalary: true);

		var sql = Translate(criteria);

		Assert.Contains("websearch_to_tsquery", sql, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("ILIKE", sql, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("job_categories", sql, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Apply_EmptyCriteria_ProducesAnUnfilteredQuery()
	{
		var sql = Translate(new AlertCriteria());

		Assert.DoesNotContain("WHERE", sql, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Apply_SubstringCriteria_EscapesLikeWildcards()
	{
		var sql = Translate(new AlertCriteria(TitleContains: "100% remote_role"));

		// The wildcards the user typed must be escaped, or their filter silently widens.
		Assert.Contains("ESCAPE", sql, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("100\\%", sql, StringComparison.Ordinal);
		Assert.Contains("remote\\_role", sql, StringComparison.Ordinal);
	}

	[Fact]
	public void Apply_SalaryBoundWithoutRequireSalary_KeepsJobsThatStateNoSalary()
	{
		var sql = Translate(new AlertCriteria(SalaryMin: 3000));

		Assert.Contains("IS NULL", sql, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Apply_EachCriterionAlone_Translates()
	{
		AlertCriteria[] singles =
		[
			new(Query: "backend"),
			new(TitleContains: "backend"),
			new(DescriptionContains: "dotnet"),
			new(CompanyContains: "acme"),
			new(CategorySlugs: ["it"]),
			new(Cities: ["Tbilisi"]),
			new(WorkModes: ["remote"]),
			new(EmploymentTypes: ["full_time"]),
			new(Seniorities: ["senior"]),
			new(Languages: ["en"]),
			new(SalaryMin: 1000),
			new(SalaryMax: 9000),
			new(RequireSalary: true)
		];

		foreach (var criteria in singles)
			Assert.False(string.IsNullOrWhiteSpace(Translate(criteria)));
	}
}
