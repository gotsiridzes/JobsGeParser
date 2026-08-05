using System.Text;

namespace JobsGeParser.PerformanceTests;

public static class PerfReportWriter
{
	public static string WriteMarkdown(IReadOnlyList<ScrapePerfResult> results, string outputDirectory)
	{
		Directory.CreateDirectory(outputDirectory);
		var path = Path.Combine(outputDirectory, $"perf-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.md");

		var sb = new StringBuilder();
		sb.AppendLine("# Live scrape performance results");
		sb.AppendLine();
		sb.AppendLine($"Generated (UTC): {DateTimeOffset.UtcNow:O}");
		sb.AppendLine();
		sb.AppendLine("Compare relative rankings from the same run. Absolute numbers vary with jobs.ge load.");
		sb.AppendLine();
		sb.AppendLine("| Scenario | DelayMs | DetailConc | CatConc | TimeoutS | Duration | JobsDisc | DetailsFetched | Details/s | Jobs/s | FailedJobs | FailedRuns |");
		sb.AppendLine("|----------|---------|------------|---------|----------|----------|----------|----------------|-----------|--------|------------|------------|");

		foreach (var r in results.OrderBy(x => x.Duration))
		{
			sb.AppendLine(
				$"| {r.ScenarioName} | {r.DetailPageDelayMs} | {r.DetailFetchConcurrency} | {r.CategoryScrapeConcurrency} | {r.HttpClientTimeoutSeconds} | {r.Duration:hh\\:mm\\:ss\\.fff} | {r.JobsDiscovered} | {r.DetailsFetched} | {r.DetailsPerSecond:F2} | {r.JobsDiscoveredPerSecond:F2} | {r.Failed} | {r.FailedRuns} |");
		}

		sb.AppendLine();
		sb.AppendLine("## Notes");
		sb.AppendLine();
		foreach (var r in results.Where(x => x.ErrorSamples.Count > 0))
		{
			sb.AppendLine($"### {r.ScenarioName} errors");
			foreach (var sample in r.ErrorSamples)
				sb.AppendLine($"- {sample}");
			sb.AppendLine();
		}

		var ranked = results
			.Where(r => r.FailedRuns == 0 && r.FailedJobRate < 0.2)
			.OrderByDescending(r => r.DetailsPerSecond)
			.ThenBy(r => r.Duration)
			.ToList();

		sb.AppendLine("## Suggested ranking (by details/s, failed job rate < 20%)");
		sb.AppendLine();
		if (ranked.Count == 0)
		{
			sb.AppendLine("No scenario met the soft success criteria.");
		}
		else
		{
			for (var i = 0; i < ranked.Count; i++)
			{
				var r = ranked[i];
				sb.AppendLine(
					$"{i + 1}. **{r.ScenarioName}**: delay={r.DetailPageDelayMs}, detailConc={r.DetailFetchConcurrency}, catConc={r.CategoryScrapeConcurrency}, timeout={r.HttpClientTimeoutSeconds}s → {r.DetailsPerSecond:F2} details/s");
			}
		}

		File.WriteAllText(path, sb.ToString());
		return path;
	}

	public static void WriteConsoleSummary(IReadOnlyList<ScrapePerfResult> results)
	{
		Console.WriteLine();
		Console.WriteLine("=== Scrape performance summary (sorted by duration) ===");
		foreach (var r in results.OrderBy(x => x.Duration))
		{
			Console.WriteLine(
				$"{r.ScenarioName,-24} {r.Duration:hh\\:mm\\:ss}  details/s={r.DetailsPerSecond:F2}  failedJobs={r.Failed}  failedRuns={r.FailedRuns}");
		}
		Console.WriteLine();
	}
}
