namespace JobsGeParser.Data;

public class JobCategoryEntity
{
	public int JobId { get; set; }

	public JobEntity Job { get; set; } = null!;

	public string CategorySlug { get; set; } = null!;

	public CategoryEntity Category { get; set; } = null!;

	public DateTimeOffset FirstSeenAt { get; set; }

	public DateTimeOffset LastSeenAt { get; set; }
}
