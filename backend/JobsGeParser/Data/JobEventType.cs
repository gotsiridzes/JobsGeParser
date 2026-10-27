namespace JobsGeParser.Data;

public static class JobEventType
{
	/// <summary>
	/// Job detail was fetched and enriched for the first time, so the row is complete enough to match filters.
	/// </summary>
	public const string JobReady = "JobReady";

	/// <summary>
	/// An already complete job had its description replaced, so enrichment ran again.
	/// </summary>
	public const string JobReEnriched = "JobReEnriched";

	/// <summary>
	/// An already complete job was linked to an additional category by a later category scrape.
	/// </summary>
	public const string JobCategoryLinked = "JobCategoryLinked";
}
