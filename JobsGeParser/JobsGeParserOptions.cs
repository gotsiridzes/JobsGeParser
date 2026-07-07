namespace JobsGeParser;

public class JobsGeParserOptions
{
	public string BaseUrl { get; set; } = null!;

	public List<JobCategoryOptions> Categories { get; set; } = [];

	public bool ScrapeEnabled { get; set; } = true;

	public int ScrapeIntervalMinutes { get; set; } = 60;

	public bool ScrapeOnStartup { get; set; }

	public int DetailPageDelayMs { get; set; } = 500;

	public IEnumerable<JobCategoryOptions> EnabledCategories =>
		Categories.Where(c => c.Enabled);
}
