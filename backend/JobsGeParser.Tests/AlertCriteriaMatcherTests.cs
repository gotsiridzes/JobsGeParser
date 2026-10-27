using JobsGeParser.Alerts;

namespace JobsGeParser.Tests;

public class AlertCriteriaMatcherTests
{
	private static JobSnapshot Job(
		string name = "Senior Backend Developer",
		string company = "Acme LLC",
		string? description = "We need a .NET engineer. Fluent English required.",
		string? city = "Tbilisi",
		string? workMode = "remote",
		string? employmentType = "full_time",
		string? seniority = "senior",
		string? language = "en",
		decimal? salaryMin = null,
		decimal? salaryMax = null,
		string? salaryCurrency = null,
		params string[] categories) =>
		new(
			1,
			name,
			company,
			"https://jobs.ge/en/?view=jobs&id=1",
			description,
			city,
			workMode,
			employmentType,
			seniority,
			language,
			salaryMin,
			salaryMax,
			salaryCurrency,
			categories.Length == 0 ? ["it"] : categories,
			DateTimeOffset.UtcNow);

	[Fact]
	public void Matches_EmptyCriteria_MatchesEverything()
	{
		Assert.True(AlertCriteriaMatcher.Matches(Job(), new AlertCriteria()));
	}

	[Theory]
	[InlineData("backend", true)]
	[InlineData("BACKEND", true)]
	[InlineData("  backend  ", true)]
	[InlineData("frontend", false)]
	public void Matches_TitleContains_IsCaseInsensitiveAndTrimmed(string needle, bool expected)
	{
		var result = AlertCriteriaMatcher.Matches(Job(), new AlertCriteria(TitleContains: needle));

		Assert.Equal(expected, result);
	}

	[Fact]
	public void Matches_GeorgianDescriptionSubstring_IsCaseInsensitive()
	{
		var job = Job(description: "პოზიცია არის დისტანციური და საინტერესო.");

		Assert.True(AlertCriteriaMatcher.Matches(job, new AlertCriteria(DescriptionContains: "დისტანციური")));
	}

	[Fact]
	public void Matches_DescriptionContains_NullDescriptionDoesNotMatch()
	{
		var job = Job(description: null);

		Assert.False(AlertCriteriaMatcher.Matches(job, new AlertCriteria(DescriptionContains: "anything")));
	}

	[Fact]
	public void Matches_CompanyContains_UsesCompanyField()
	{
		Assert.True(AlertCriteriaMatcher.Matches(Job(), new AlertCriteria(CompanyContains: "acme")));
		Assert.False(AlertCriteriaMatcher.Matches(Job(), new AlertCriteria(CompanyContains: "globex")));
	}

	[Fact]
	public void Matches_EmptyArray_MeansAny()
	{
		var criteria = new AlertCriteria(WorkModes: []);

		Assert.True(AlertCriteriaMatcher.Matches(Job(workMode: "onsite"), criteria));
	}

	[Theory]
	[InlineData("remote", true)]
	[InlineData("onsite", false)]
	public void Matches_WorkMode_NarrowsToListedValues(string wanted, bool expected)
	{
		var result = AlertCriteriaMatcher.Matches(Job(workMode: "remote"), new AlertCriteria(WorkModes: [wanted]));

		Assert.Equal(expected, result);
	}

	[Fact]
	public void Matches_UnenrichedFacet_DoesNotSatisfyAnExplicitFilter()
	{
		var job = Job(seniority: null);

		Assert.False(AlertCriteriaMatcher.Matches(job, new AlertCriteria(Seniorities: ["senior"])));
	}

	[Fact]
	public void Matches_Category_MatchesWhenAnySlugOverlaps()
	{
		var job = Job(categories: ["finance-statistics", "it"]);

		Assert.True(AlertCriteriaMatcher.Matches(job, new AlertCriteria(CategorySlugs: ["it"])));
		Assert.False(AlertCriteriaMatcher.Matches(job, new AlertCriteria(CategorySlugs: ["sales"])));
	}

	[Fact]
	public void Matches_AllCriteria_AreCombinedWithAnd()
	{
		var job = Job(workMode: "remote", seniority: "senior");
		var criteria = new AlertCriteria(
			TitleContains: "backend",
			WorkModes: ["remote"],
			Seniorities: ["junior"]);

		Assert.False(AlertCriteriaMatcher.Matches(job, criteria));
	}

	[Fact]
	public void Matches_SalaryBound_IgnoresJobsWithoutSalaryByDefault()
	{
		var job = Job(salaryMin: null, salaryMax: null);

		Assert.True(AlertCriteriaMatcher.Matches(job, new AlertCriteria(SalaryMin: 5000)));
	}

	[Fact]
	public void Matches_RequireSalary_ExcludesJobsWithoutSalary()
	{
		var job = Job(salaryMin: null, salaryMax: null);

		Assert.False(AlertCriteriaMatcher.Matches(job, new AlertCriteria(RequireSalary: true)));
	}

	[Theory]
	[InlineData(2000, 4000, 3000, true)]
	[InlineData(2000, 4000, 4000, true)]
	[InlineData(2000, 4000, 4500, false)]
	public void Matches_SalaryMin_UsesTopOfTheJobRange(
		decimal jobMin,
		decimal jobMax,
		decimal wanted,
		bool expected)
	{
		var job = Job(salaryMin: jobMin, salaryMax: jobMax);

		var result = AlertCriteriaMatcher.Matches(job, new AlertCriteria(SalaryMin: wanted));

		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData(2000, 4000, 2500, true)]
	[InlineData(2000, 4000, 1500, false)]
	public void Matches_SalaryMax_UsesBottomOfTheJobRange(
		decimal jobMin,
		decimal jobMax,
		decimal ceiling,
		bool expected)
	{
		var job = Job(salaryMin: jobMin, salaryMax: jobMax);

		var result = AlertCriteriaMatcher.Matches(job, new AlertCriteria(SalaryMax: ceiling));

		Assert.Equal(expected, result);
	}

	[Fact]
	public void Matches_SalarySinglePoint_IsTreatedAsBothEnds()
	{
		var job = Job(salaryMin: 3000, salaryMax: null);

		Assert.True(AlertCriteriaMatcher.Matches(job, new AlertCriteria(SalaryMin: 3000)));
		Assert.False(AlertCriteriaMatcher.Matches(job, new AlertCriteria(SalaryMin: 3001)));
	}

	[Fact]
	public void Matches_SalaryCurrency_MustAgreeWhenTheJobStatesOne()
	{
		var job = Job(salaryMin: 3000, salaryCurrency: "GEL");

		Assert.True(AlertCriteriaMatcher.Matches(job, new AlertCriteria(SalaryMin: 1000, SalaryCurrency: "gel")));
		Assert.False(AlertCriteriaMatcher.Matches(job, new AlertCriteria(SalaryMin: 1000, SalaryCurrency: "USD")));
	}
}
