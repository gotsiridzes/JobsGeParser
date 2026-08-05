namespace JobsGeParser.Endpoints.Dtos;

public record JobQuery(string? CategorySlug, string? Search, bool DotNetOnly);

public record JobListItemDto(
	int Id,
	string Name,
	string Link,
	string Company,
	string? CompanyLink,
	DateOnly Published,
	DateOnly EndDate,
	DateTimeOffset LastSeenAt,
	decimal? SalaryMin = null,
	decimal? SalaryMax = null,
	string? SalaryCurrency = null,
	string? SalaryPeriod = null,
	string? City = null,
	string? WorkMode = null,
	string? EmploymentType = null,
	string? Seniority = null,
	string? LanguageRequirement = null);

public record JobDetailDto(
	int Id,
	string Name,
	string Link,
	string Company,
	string? CompanyLink,
	DateOnly Published,
	DateOnly EndDate,
	string? Description,
	DateTimeOffset? DetailsFetchedAt,
	DateTimeOffset FirstSeenAt,
	DateTimeOffset LastSeenAt,
	DateTimeOffset? UpdatedAt,
	IReadOnlyList<string> CategorySlugs,
	decimal? SalaryMin = null,
	decimal? SalaryMax = null,
	string? SalaryCurrency = null,
	string? SalaryPeriod = null,
	string? City = null,
	string? WorkMode = null,
	string? EmploymentType = null,
	string? Seniority = null,
	string? LanguageRequirement = null,
	int EnrichmentVersion = 0,
	DateTimeOffset? EnrichedAt = null);

public record JobsPageDto(
	IReadOnlyList<JobListItemDto> Items,
	int TotalCount,
	int Page,
	int PageSize,
	int TotalPages);

public record SearchResultDto(
	int Id,
	string Name,
	string Link,
	string Company,
	string? CompanyLink,
	DateOnly Published,
	DateOnly EndDate,
	DateTimeOffset LastSeenAt,
	decimal? Rank,
	decimal? SalaryMin,
	decimal? SalaryMax,
	string? SalaryCurrency,
	string? SalaryPeriod,
	string? City,
	string? WorkMode,
	string? EmploymentType,
	string? Seniority,
	string? LanguageRequirement);

public record SearchPageDto(
	IReadOnlyList<SearchResultDto> Items,
	int TotalCount,
	int Page,
	int PageSize,
	int TotalPages,
	string Mode);

public record EnrichmentBackfillResultDto(int Processed, int Remaining);
