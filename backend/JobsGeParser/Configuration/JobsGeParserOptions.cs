namespace JobsGeParser.Configuration;

public class JobsGeParserOptions
{
	public string BaseUrl { get; set; } = null!;

	public List<JobCategoryOptions> Categories { get; set; } = [];

	public bool ScrapeEnabled { get; set; } = true;

	public int ScrapeIntervalMinutes { get; set; } = 60;

	public bool ScrapeOnStartup { get; set; }

	public int DetailPageDelayMs { get; set; } = 500;

	public int DetailFetchConcurrency { get; set; } = 3;

	public int CategoryScrapeConcurrency { get; set; } = 5;

	public int ProgressUpdateInterval { get; set; } = 5;

	public int DefaultJobsPageSize { get; set; } = 20;

	public int MaxJobsPageSize { get; set; } = 100;

	public IEnumerable<JobCategoryOptions> EnabledCategories =>
		Categories.Where(c => c.Enabled);
}
