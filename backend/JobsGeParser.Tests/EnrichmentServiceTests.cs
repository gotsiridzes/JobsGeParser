using JobsGeParser.Enrichment;

namespace JobsGeParser.Tests;

public class EnrichmentServiceTests
{
	private readonly EnrichmentService _sut = new();

	[Fact]
	public void Extract_SalaryRangeWithGel_ParsesMinMaxCurrencyAndPeriod()
	{
		var result = _sut.Extract(
			"Backend Developer",
			"Salary: 3000-4500 GEL per month. Office in Tbilisi.");

		Assert.Equal(3000m, result.SalaryMin);
		Assert.Equal(4500m, result.SalaryMax);
		Assert.Equal("GEL", result.SalaryCurrency);
		Assert.Equal("month", result.SalaryPeriod);
		Assert.Equal("Tbilisi", result.City);
	}

	[Fact]
	public void Extract_GeorgianRemoteKeyword_SetsWorkMode()
	{
		var result = _sut.Extract(
			"Developer",
			"პოზიცია არის დისტანციური. Fluent English required.");

		Assert.Equal("remote", result.WorkMode);
		Assert.Equal("en", result.LanguageRequirement);
	}

	[Fact]
	public void Extract_SeniorityInTitle_SetsSeniority()
	{
		var result = _sut.Extract(
			"Senior .NET Engineer",
			"Full-time role in Batumi.");

		Assert.Equal("senior", result.Seniority);
		Assert.Equal("full_time", result.EmploymentType);
		Assert.Equal("Batumi", result.City);
	}

	[Fact]
	public void Extract_InternshipKeyword_SetsEmploymentTypeAndSeniority()
	{
		var result = _sut.Extract(
			"Software Intern",
			"Paid internship, hybrid work.");

		Assert.Equal("intern", result.Seniority);
		Assert.Equal("internship", result.EmploymentType);
		Assert.Equal("hybrid", result.WorkMode);
	}

	[Fact]
	public void Extract_UsdHourly_ParsesCurrencyAndPeriod()
	{
		var result = _sut.Extract(
			"Contractor",
			"Rate $50 per hour, remote.");

		Assert.Equal(50m, result.SalaryMin);
		Assert.Equal(50m, result.SalaryMax);
		Assert.Equal("USD", result.SalaryCurrency);
		Assert.Equal("hour", result.SalaryPeriod);
		Assert.Equal("remote", result.WorkMode);
	}

	[Fact]
	public void Extract_EmptyDescription_ReturnsNulls()
	{
		var result = _sut.Extract("Developer", null);

		Assert.Null(result.SalaryMin);
		Assert.Null(result.WorkMode);
		Assert.Null(result.City);
	}

	[Fact]
	public void CurrentVersion_IsPositive()
	{
		Assert.True(EnrichmentService.CurrentVersion >= 1);
	}
}
