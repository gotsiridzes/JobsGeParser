using JobsGeParser.Alerts;

namespace JobsGeParser.Tests;

public class AlertCriteriaTests
{
	[Fact]
	public void Canonicalize_TrimsSortsDedupesAndNullsEmpties()
	{
		var criteria = new AlertCriteria(
			TitleContains: "  backend  ",
			Query: "   ",
			WorkModes: ["remote", " REMOTE ", "hybrid"],
			Cities: []);

		var canonical = criteria.Canonicalize();

		Assert.Equal("backend", canonical.TitleContains);
		Assert.Null(canonical.Query);
		Assert.Null(canonical.Cities);
		Assert.Equal(["hybrid", "remote"], canonical.WorkModes);
	}

	[Fact]
	public void ComputeHash_IsStableAcrossOrderCaseAndWhitespace()
	{
		var a = new AlertCriteria(TitleContains: "Backend", WorkModes: ["remote", "hybrid"]);
		var b = new AlertCriteria(TitleContains: " backend ", WorkModes: ["HYBRID", "remote"]);

		Assert.Equal(a.ComputeHash(), b.ComputeHash());
	}

	[Fact]
	public void ComputeHash_DiffersWhenACriterionDiffers()
	{
		var a = new AlertCriteria(TitleContains: "backend");
		var b = new AlertCriteria(TitleContains: "frontend");

		Assert.NotEqual(a.ComputeHash(), b.ComputeHash());
	}

	[Fact]
	public void ComputeHash_DistinguishesFieldBoundaries()
	{
		// Without a separator between fields these two would serialize identically.
		var a = new AlertCriteria(TitleContains: "ab", DescriptionContains: "c");
		var b = new AlertCriteria(TitleContains: "a", DescriptionContains: "bc");

		Assert.NotEqual(a.ComputeHash(), b.ComputeHash());
	}

	[Fact]
	public void ComputeHash_DistinguishesRequireSalary()
	{
		var a = new AlertCriteria(SalaryMin: 3000);
		var b = new AlertCriteria(SalaryMin: 3000, RequireSalary: true);

		Assert.NotEqual(a.ComputeHash(), b.ComputeHash());
	}

	[Fact]
	public void IsEmpty_IgnoresRequireSalaryAndCurrencyAlone()
	{
		Assert.True(new AlertCriteria(RequireSalary: true, SalaryCurrency: "GEL").IsEmpty);
		Assert.False(new AlertCriteria(SalaryMin: 1).IsEmpty);
	}
}
