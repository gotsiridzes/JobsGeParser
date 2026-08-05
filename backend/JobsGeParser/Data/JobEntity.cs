using NpgsqlTypes;

namespace JobsGeParser.Data;

public class JobEntity
{
	public int Id { get; set; }

	public string Name { get; set; } = null!;

	public string Link { get; set; } = null!;

	public string Company { get; set; } = null!;

	public string? CompanyLink { get; set; }

	public DateOnly Published { get; set; }

	public DateOnly EndDate { get; set; }

	public string? Description { get; set; }

	public string? DescriptionHtml { get; set; }

	public NpgsqlTsVector SearchVector { get; set; } = null!;

	public decimal? SalaryMin { get; set; }

	public decimal? SalaryMax { get; set; }

	public string? SalaryCurrency { get; set; }

	public string? SalaryPeriod { get; set; }

	public string? City { get; set; }

	public string? WorkMode { get; set; }

	public string? EmploymentType { get; set; }

	public string? Seniority { get; set; }

	public string? LanguageRequirement { get; set; }

	public int EnrichmentVersion { get; set; }

	public DateTimeOffset? EnrichedAt { get; set; }

	public DateTimeOffset? DetailsFetchedAt { get; set; }

	public DateTimeOffset FirstSeenAt { get; set; }

	public DateTimeOffset LastSeenAt { get; set; }

	public DateTimeOffset? UpdatedAt { get; set; }

	public ICollection<JobCategoryEntity> JobCategories { get; set; } = [];
}
