namespace JobsGeParser.Models;

public record ScrapeResult(int Inserted, int Updated, int Skipped, int Failed, TimeSpan Duration);
