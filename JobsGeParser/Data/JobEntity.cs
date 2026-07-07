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

	public DateTimeOffset FirstSeenAt { get; set; }

	public DateTimeOffset LastSeenAt { get; set; }

	public DateTimeOffset? UpdatedAt { get; set; }

	public ICollection<JobCategoryEntity> JobCategories { get; set; } = [];
}
