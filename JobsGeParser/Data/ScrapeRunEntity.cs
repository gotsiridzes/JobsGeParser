namespace JobsGeParser.Data;

public class ScrapeRunEntity
{
	public long Id { get; set; }

	public Guid? BatchId { get; set; }

	public string? CategorySlug { get; set; }

	public CategoryEntity? Category { get; set; }

	public DateTimeOffset StartedAt { get; set; }

	public DateTimeOffset? FinishedAt { get; set; }

	public int Inserted { get; set; }

	public int Updated { get; set; }

	public int Skipped { get; set; }

	public int Failed { get; set; }

	public string Status { get; set; } = null!;

	public string? ErrorMessage { get; set; }
}
