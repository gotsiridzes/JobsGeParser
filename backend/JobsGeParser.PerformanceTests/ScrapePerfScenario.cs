namespace JobsGeParser.PerformanceTests;

public sealed record ScrapePerfScenario(
	string Name,
	int DetailPageDelayMs,
	int DetailFetchConcurrency,
	int CategoryScrapeConcurrency,
	int HttpClientTimeoutSeconds,
	int MaxListingPages = 2,
	bool AllCategories = false);
