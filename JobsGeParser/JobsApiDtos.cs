namespace JobsGeParser;

public record JobQuery(string? CategorySlug, string? Search, bool DotNetOnly);

public record JobListItemDto(
	int Id,
	string Name,
	string Link,
	string Company,
	string? CompanyLink,
	DateOnly Published,
	DateOnly EndDate,
	DateTimeOffset LastSeenAt);

public record JobDetailDto(
	int Id,
	string Name,
	string Link,
	string Company,
	string? CompanyLink,
	DateOnly Published,
	DateOnly EndDate,
	string? Description,
	DateTimeOffset FirstSeenAt,
	DateTimeOffset LastSeenAt,
	DateTimeOffset? UpdatedAt,
	IReadOnlyList<string> CategorySlugs);

public record JobsPageDto(
	IReadOnlyList<JobListItemDto> Items,
	int TotalCount,
	int Page,
	int PageSize,
	int TotalPages);
