namespace JobsGeParser.Configuration;

public class JobCategoryOptions
{
	public string Slug { get; set; } = null!;

	public string Name { get; set; } = null!;

	public string ListUrl { get; set; } = null!;

	public bool Enabled { get; set; } = true;
}
