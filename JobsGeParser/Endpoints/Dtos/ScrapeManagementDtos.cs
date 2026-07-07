using JobsGeParser.Data;
using JobsGeParser.Workers;

namespace JobsGeParser.Endpoints.Dtos;

public record CategoryDto(
	string Slug,
	string Name,
	string ListUrl,
	bool Enabled,
	int JobCount,
	ScrapeRunEntity? LatestScrapeRun);

public record ScrapeWorkerStatusDto(
	bool ScrapeEnabled,
	int ScrapeIntervalMinutes,
	bool ScrapeOnStartup,
	ScrapeWorkerSnapshot Worker);

public record ScrapeRunsPageDto(
	IReadOnlyList<ScrapeRunEntity> Items,
	int TotalCount,
	int Limit,
	int Offset);

public record ScrapeBatchSummaryDto(
	Guid BatchId,
	DateTimeOffset StartedAt,
	DateTimeOffset? FinishedAt,
	int TotalRuns,
	int Running,
	int Completed,
	int Failed,
	IReadOnlyList<ScrapeRunEntity> Runs);

public record ScrapeOverviewDto(
	ScrapeWorkerStatusDto Worker,
	IReadOnlyList<ScrapeRunEntity> ActiveRuns,
	IReadOnlyList<ScrapeRunEntity> LatestPerCategory,
	IReadOnlyList<ScrapeRunEntity> RecentRuns,
	IReadOnlyList<ScrapeBatchSummaryDto> RecentBatches);
