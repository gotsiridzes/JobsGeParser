namespace JobsGeParser.PerformanceTests;

[Collection("ScrapePerf")]
[Trait("Category", "Performance")]
public sealed class ScrapePerformanceMatrixTests(ScrapePerformanceFixture fixture)
{
	public static TheoryData<ScrapePerfScenario> MatrixScenarios =>
	[
		new("baseline_current", 300, 25, 8, 30),
		new("conservative", 500, 3, 2, 30),
		new("low_delay_low_conc", 100, 3, 2, 30),
		new("mid_balanced", 200, 8, 4, 30),
		new("high_conc_with_delay", 300, 16, 4, 30),
		new("max_aggressive", 0, 25, 8, 30),
		new("cat_bottleneck", 200, 16, 1, 30),
		new("detail_bottleneck", 200, 1, 8, 30),
		new("short_timeout", 300, 8, 4, 15),
		new("long_timeout", 300, 8, 4, 60),
	];

	[Theory]
	[MemberData(nameof(MatrixScenarios))]
	public async Task ScrapeBatch_WithSettings_CompletesAndRecordsMetrics(ScrapePerfScenario scenario)
	{
		var result = await fixture.RunScenarioAsync(scenario);

		Assert.True(result.CompletedRuns + result.FailedRuns > 0, "Expected at least one scrape run.");
		Assert.True(
			result.FailedJobRate < 0.2 || result.JobsDiscovered == 0,
			$"Failed job rate {result.FailedJobRate:P1} exceeded 20% soft threshold for {scenario.Name}.");
	}
}

[Collection("ScrapePerf")]
[Trait("Category", "PerformanceHeavy")]
public sealed class ScrapePerformanceHeavyTests(ScrapePerformanceFixture fixture)
{
	[Fact]
	public async Task NearProduction_BaselineSettings_AllCategories()
	{
		var scenario = new ScrapePerfScenario(
			Name: "near_production_baseline",
			DetailPageDelayMs: 300,
			DetailFetchConcurrency: 25,
			CategoryScrapeConcurrency: 8,
			HttpClientTimeoutSeconds: 30,
			MaxListingPages: 5,
			AllCategories: true);

		var result = await fixture.RunScenarioAsync(scenario);

		Assert.True(result.CompletedRuns + result.FailedRuns > 0);
		Assert.True(
			result.FailedJobRate < 0.2 || result.JobsDiscovered == 0,
			$"Failed job rate {result.FailedJobRate:P1} exceeded 20% soft threshold.");
	}
}
