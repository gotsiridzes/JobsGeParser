using JobsGeParser.Configuration;
using JobsGeParser.Data;
using Microsoft.EntityFrameworkCore;

namespace JobsGeParser.Scraping;

public static class CategorySync
{
	public static async Task SyncAsync(
		JobsDbContext db,
		JobsGeParserOptions options,
		CancellationToken ct = default)
	{
		var configSlugs = options.Categories.Select(c => c.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);

		foreach (var category in options.Categories)
		{
			var existing = await db.Categories.FindAsync([category.Slug], ct);
			if (existing is null)
			{
				db.Categories.Add(new CategoryEntity
				{
					Slug = category.Slug,
					Name = category.Name,
					ListUrl = category.ListUrl,
					Enabled = category.Enabled
				});
			}
			else
			{
				existing.Name = category.Name;
				existing.ListUrl = category.ListUrl;
				existing.Enabled = category.Enabled;
			}
		}

		var removed = await db.Categories
			.Where(c => !configSlugs.Contains(c.Slug))
			.ToListAsync(ct);

		foreach (var category in removed)
			category.Enabled = false;

		await db.SaveChangesAsync(ct);

		await BackfillJobsToCategoryAsync(db, "it", ct);
	}

	private static async Task BackfillJobsToCategoryAsync(
		JobsDbContext db,
		string categorySlug,
		CancellationToken ct)
	{
		if (!await db.Categories.AnyAsync(c => c.Slug == categorySlug, ct))
			return;

		var jobIdsWithoutCategory = await db.Jobs
			.Where(j => !j.JobCategories.Any())
			.Select(j => j.Id)
			.ToListAsync(ct);

		if (jobIdsWithoutCategory.Count == 0)
			return;

		var now = DateTimeOffset.UtcNow;
		foreach (var jobId in jobIdsWithoutCategory)
		{
			db.JobCategories.Add(new JobCategoryEntity
			{
				JobId = jobId,
				CategorySlug = categorySlug,
				FirstSeenAt = now,
				LastSeenAt = now
			});
		}

		await db.SaveChangesAsync(ct);
	}
}
