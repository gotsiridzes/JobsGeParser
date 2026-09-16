using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JobsGeParser.Data;

namespace JobsGeParser.Alerts;

/// <summary>
/// A saved filter, detached from storage. The subscription entity holds the same fields as
/// columns; this record is what the matcher, the query builder and the validator all work with,
/// so a future saved-search feature can reuse the logic without reusing the table.
/// </summary>
public sealed record AlertCriteria(
	string? Query = null,
	string? TitleContains = null,
	string? DescriptionContains = null,
	string? CompanyContains = null,
	IReadOnlyList<string>? CategorySlugs = null,
	IReadOnlyList<string>? Cities = null,
	IReadOnlyList<string>? WorkModes = null,
	IReadOnlyList<string>? EmploymentTypes = null,
	IReadOnlyList<string>? Seniorities = null,
	IReadOnlyList<string>? Languages = null,
	decimal? SalaryMin = null,
	decimal? SalaryMax = null,
	string? SalaryCurrency = null,
	bool RequireSalary = false)
{
	// Separators keep "ab" plus "c" from hashing the same as "a" plus "bc".
	private const char FieldSeparator = (char)31;
	private const char GroupSeparator = (char)30;

	public static AlertCriteria From(AlertSubscriptionEntity subscription) => new(
		subscription.Query,
		subscription.TitleContains,
		subscription.DescriptionContains,
		subscription.CompanyContains,
		subscription.CategorySlugs,
		subscription.Cities,
		subscription.WorkModes,
		subscription.EmploymentTypes,
		subscription.Seniorities,
		subscription.Languages,
		subscription.SalaryMin,
		subscription.SalaryMax,
		subscription.SalaryCurrency,
		subscription.RequireSalary);

	/// <summary>
	/// True when nothing is set, which would match every job. Callers reject that.
	/// </summary>
	public bool IsEmpty =>
		string.IsNullOrWhiteSpace(Query)
		&& string.IsNullOrWhiteSpace(TitleContains)
		&& string.IsNullOrWhiteSpace(DescriptionContains)
		&& string.IsNullOrWhiteSpace(CompanyContains)
		&& IsUnset(CategorySlugs)
		&& IsUnset(Cities)
		&& IsUnset(WorkModes)
		&& IsUnset(EmploymentTypes)
		&& IsUnset(Seniorities)
		&& IsUnset(Languages)
		&& SalaryMin is null
		&& SalaryMax is null;

	/// <summary>
	/// Normalizes the shape stored on a subscription: trimmed strings, null for blanks, arrays
	/// trimmed, deduped, sorted and null when empty. Two filters that mean the same thing then
	/// hash the same, which is what the per-subscriber uniqueness index relies on.
	/// </summary>
	public AlertCriteria Canonicalize() => new(
		Clean(Query),
		Clean(TitleContains),
		Clean(DescriptionContains),
		Clean(CompanyContains),
		CleanList(CategorySlugs),
		CleanList(Cities),
		CleanList(WorkModes),
		CleanList(EmploymentTypes),
		CleanList(Seniorities),
		CleanList(Languages),
		SalaryMin,
		SalaryMax,
		Clean(SalaryCurrency),
		RequireSalary);

	/// <summary>
	/// SHA-256 over the canonical form. Case insensitive, because matching is.
	/// </summary>
	public byte[] ComputeHash()
	{
		var criteria = Canonicalize();
		var builder = new StringBuilder();
		Append(builder, criteria.Query);
		Append(builder, criteria.TitleContains);
		Append(builder, criteria.DescriptionContains);
		Append(builder, criteria.CompanyContains);
		Append(builder, criteria.CategorySlugs);
		Append(builder, criteria.Cities);
		Append(builder, criteria.WorkModes);
		Append(builder, criteria.EmploymentTypes);
		Append(builder, criteria.Seniorities);
		Append(builder, criteria.Languages);
		Append(builder, criteria.SalaryMin?.ToString(CultureInfo.InvariantCulture));
		Append(builder, criteria.SalaryMax?.ToString(CultureInfo.InvariantCulture));
		Append(builder, criteria.SalaryCurrency);
		Append(builder, criteria.RequireSalary ? "1" : "0");

		return SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
	}

	internal static bool IsUnset(IReadOnlyList<string>? values) => values is null || values.Count == 0;

	private static string? Clean(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	private static IReadOnlyList<string>? CleanList(IReadOnlyList<string>? values)
	{
		if (values is null)
			return null;

		var cleaned = values
			.Where(v => !string.IsNullOrWhiteSpace(v))
			.Select(v => v.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		return cleaned.Length == 0 ? null : cleaned;
	}

	private static void Append(StringBuilder builder, string? value) =>
		builder.Append(value?.ToLowerInvariant()).Append(FieldSeparator);

	private static void Append(StringBuilder builder, IReadOnlyList<string>? values)
	{
		if (values is not null)
		{
			foreach (var value in values)
				builder.Append(value.ToLowerInvariant()).Append(FieldSeparator);
		}

		builder.Append(GroupSeparator);
	}
}
