using System.Globalization;
using System.Text;

namespace JobsGeParser.Alerts.Rendering;

/// <summary>
/// Shared, channel neutral formatting of the small facts that appear on a job line.
/// </summary>
public static class AlertJobFormatting
{
	/// <summary>
	/// Renders salary as a single readable span, or null when the job states none.
	/// </summary>
	public static string? Salary(AlertJobModel job)
	{
		if (job.SalaryMin is null && job.SalaryMax is null)
			return null;

		var currency = string.IsNullOrWhiteSpace(job.SalaryCurrency) ? "" : $" {job.SalaryCurrency}";

		if (job.SalaryMin is { } min && job.SalaryMax is { } max && min != max)
			return $"{Amount(min)} to {Amount(max)}{currency}";

		return $"{Amount(job.SalaryMin ?? job.SalaryMax!.Value)}{currency}";
	}

	/// <summary>
	/// The short facts shown under a title, skipping anything the extractor could not determine.
	/// </summary>
	public static string Facts(AlertJobModel job, string separator = " | ")
	{
		var parts = new List<string>(4);

		if (!string.IsNullOrWhiteSpace(job.City))
			parts.Add(job.City);

		if (!string.IsNullOrWhiteSpace(job.WorkMode))
			parts.Add(job.WorkMode);

		if (!string.IsNullOrWhiteSpace(job.Seniority))
			parts.Add(job.Seniority);

		if (Salary(job) is { } salary)
			parts.Add(salary);

		return string.Join(separator, parts);
	}

	/// <summary>
	/// Truncates on a word boundary so a cap never cuts a word in half.
	/// </summary>
	public static string Truncate(string value, int maxLength)
	{
		if (value.Length <= maxLength)
			return value;

		if (maxLength <= 1)
			return value[..Math.Max(maxLength, 0)];

		var slice = value[..(maxLength - 1)];
		var lastSpace = slice.LastIndexOf(' ');

		// Only break on a space when it is not so early that the result says nothing.
		if (lastSpace > maxLength / 2)
			slice = slice[..lastSpace];

		return new StringBuilder(slice.TrimEnd()).Append('…').ToString();
	}

	private static string Amount(decimal value) =>
		value == decimal.Truncate(value)
			? ((long)value).ToString("N0", CultureInfo.InvariantCulture)
			: value.ToString("N2", CultureInfo.InvariantCulture);
}
