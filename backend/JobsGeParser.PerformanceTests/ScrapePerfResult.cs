namespace JobsGeParser.PerformanceTests;

public sealed record ScrapePerfResult(
	string ScenarioName,
	int DetailPageDelayMs,
	int DetailFetchConcurrency,
	int CategoryScrapeConcurrency,
	int HttpClientTimeoutSeconds,
	int MaxListingPages,
	int CategoryCount,
	TimeSpan Duration,
	int Inserted,
	int Updated,
	int Skipped,
	int Failed,
	int DetailsFetched,
	int DetailsSkipped,
	int JobsDiscovered,
	int CompletedRuns,
	int FailedRuns,
	IReadOnlyList<string> ErrorSamples)
{
	public double DetailsPerSecond =>
		Duration.TotalSeconds > 0 ? DetailsFetched / Duration.TotalSeconds : 0;

	public double JobsDiscoveredPerSecond =>
		Duration.TotalSeconds > 0 ? JobsDiscovered / Duration.TotalSeconds : 0;

	public double FailedJobRate =>
		JobsDiscovered > 0 ? (double)Failed / JobsDiscovered : 0;
}
