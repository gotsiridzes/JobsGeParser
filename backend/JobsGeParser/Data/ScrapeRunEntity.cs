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

	public int DetailsFetched { get; set; }

	public int DetailsSkipped { get; set; }

	public string Phase { get; set; } = null!;

	public int ListingPagesFetched { get; set; }

	public int JobsDiscovered { get; set; }

	public int JobsNeedingDetails { get; set; }

	public DateTimeOffset? ProgressUpdatedAt { get; set; }

	public string Status { get; set; } = null!;

	public string? ErrorMessage { get; set; }
}
