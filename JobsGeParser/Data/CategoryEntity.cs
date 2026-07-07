namespace JobsGeParser.Data;

public class CategoryEntity
{
	public string Slug { get; set; } = null!;

	public string Name { get; set; } = null!;

	public string ListUrl { get; set; } = null!;

	public bool Enabled { get; set; }

	public ICollection<JobCategoryEntity> JobCategories { get; set; } = [];

	public ICollection<ScrapeRunEntity> ScrapeRuns { get; set; } = [];
}
