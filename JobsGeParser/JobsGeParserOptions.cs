namespace JobsGeParser;

public class JobsGeParserOptions
{
	public string BaseUrl { get; set; } = null!;

	public string JobsListUrl { get; set; } = null!;

	public bool ScrapeEnabled { get; set; } = true;

	public int ScrapeIntervalMinutes { get; set; } = 60;

	public bool ScrapeOnStartup { get; set; }

	public int DetailPageDelayMs { get; set; } = 500;
}
