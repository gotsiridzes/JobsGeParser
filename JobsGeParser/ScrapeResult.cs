namespace JobsGeParser;

public record ScrapeResult(int Inserted, int Updated, int Skipped, int Failed, TimeSpan Duration);
