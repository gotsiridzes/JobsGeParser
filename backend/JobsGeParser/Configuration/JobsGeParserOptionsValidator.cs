using Microsoft.Extensions.Options;

namespace JobsGeParser.Configuration;

public sealed class JobsGeParserOptionsValidator : IValidateOptions<JobsGeParserOptions>
{
	public ValidateOptionsResult Validate(string? name, JobsGeParserOptions options)
	{
		if (options.BaseUrl is null)
			return ValidateOptionsResult.Fail("JobsGeParserOptions.BaseUrl is required.");

		if (options.Categories is null || options.Categories.Count == 0)
			return ValidateOptionsResult.Fail("At least one category is required.");

		if (!options.Categories.Any(c => c.Enabled))
			return ValidateOptionsResult.Fail("At least one enabled category is required.");

		var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var category in options.Categories)
		{
			if (string.IsNullOrWhiteSpace(category.Slug))
				return ValidateOptionsResult.Fail("Category slug is required.");

			if (!slugs.Add(category.Slug))
				return ValidateOptionsResult.Fail($"Duplicate category slug: {category.Slug}");

			if (string.IsNullOrWhiteSpace(category.Name))
				return ValidateOptionsResult.Fail($"Category name is required for slug: {category.Slug}");

			if (string.IsNullOrWhiteSpace(category.ListUrl))
				return ValidateOptionsResult.Fail($"Category list URL is required for slug: {category.Slug}");
		}

		if (options.ScrapeIntervalMinutes < 1)
			return ValidateOptionsResult.Fail("ScrapeIntervalMinutes must be at least 1 minute.");

		if (options.DetailPageDelayMs < 0)
			return ValidateOptionsResult.Fail("DetailPageDelayMs cannot be negative.");

		if (options.DetailFetchConcurrency < 1)
			return ValidateOptionsResult.Fail("DetailFetchConcurrency must be at least 1.");

		if (options.CategoryScrapeConcurrency < 1)
			return ValidateOptionsResult.Fail("CategoryScrapeConcurrency must be at least 1.");

		if (options.HttpClientTimeoutSeconds < 1)
			return ValidateOptionsResult.Fail("HttpClientTimeoutSeconds must be at least 1.");

		if (options.MaxListingPages < 1)
			return ValidateOptionsResult.Fail("MaxListingPages must be at least 1.");

		if (options.ProgressUpdateInterval < 1)
			return ValidateOptionsResult.Fail("ProgressUpdateInterval must be at least 1.");

		if (options.DefaultJobsPageSize < 1)
			return ValidateOptionsResult.Fail("DefaultJobsPageSize must be at least 1.");

		if (options.MaxJobsPageSize < 1)
			return ValidateOptionsResult.Fail("MaxJobsPageSize must be at least 1.");

		if (options.DefaultJobsPageSize > options.MaxJobsPageSize)
			return ValidateOptionsResult.Fail("DefaultJobsPageSize cannot exceed MaxJobsPageSize.");

		return ValidateOptionsResult.Success;
	}
}
