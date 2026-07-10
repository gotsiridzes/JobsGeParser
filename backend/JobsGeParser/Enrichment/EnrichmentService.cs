namespace JobsGeParser.Enrichment;

public sealed record EnrichmentResult(
	decimal? SalaryMin,
	decimal? SalaryMax,
	string? SalaryCurrency,
	string? SalaryPeriod,
	string? City,
	string? WorkMode,
	string? EmploymentType,
	string? Seniority,
	string? LanguageRequirement);

public sealed class EnrichmentService
{
	public const int CurrentVersion = 1;

	private static readonly (string Keyword, string Value)[] WorkModeKeywords =
	[
		("დისტანციური", "remote"),
		("დისტანციურად", "remote"),
		("remote", "remote"),
		("work from home", "remote"),
		("wfh", "remote"),
		("ჰიბრიდული", "hybrid"),
		("hybrid", "hybrid"),
		("ოფისში", "onsite"),
		("ოფისიდან", "onsite"),
		("on-site", "onsite"),
		("onsite", "onsite"),
		("in office", "onsite"),
	];

	private static readonly (string Keyword, string Value)[] EmploymentTypeKeywords =
	[
		("სრული განაკვეთი", "full_time"),
		("full-time", "full_time"),
		("full time", "full_time"),
		("fulltime", "full_time"),
		("ნახევარი განაკვეთი", "part_time"),
		("part-time", "part_time"),
		("part time", "part_time"),
		("parttime", "part_time"),
		("კონტრაქტი", "contract"),
		("contract", "contract"),
		("freelance", "contract"),
		("ფრილანსი", "contract"),
		("სტაჟირება", "internship"),
		("internship", "internship"),
		("intern", "internship"),
	];

	private static readonly (string Keyword, string Value)[] SeniorityKeywords =
	[
		("intern", "intern"),
		("სტაჟიორი", "intern"),
		("junior", "junior"),
		("ჯუნიორ", "junior"),
		("entry level", "junior"),
		("entry-level", "junior"),
		("mid-level", "mid"),
		("mid level", "mid"),
		("middle", "mid"),
		("მიდლ", "mid"),
		("senior", "senior"),
		("სენიორ", "senior"),
		("lead", "lead"),
		("ლიდი", "lead"),
		("principal", "lead"),
		("head of", "lead"),
	];

	private static readonly (string Keyword, string Value)[] LanguageKeywords =
	[
		("ინგლისური", "en"),
		("english", "en"),
		("ქართული", "ka"),
		("georgian", "ka"),
		("რუსული", "ru"),
		("russian", "ru"),
		("გერმანული", "de"),
		("german", "de"),
		("ფრანგული", "fr"),
		("french", "fr"),
	];

	private static readonly (string Keyword, string Value)[] CityKeywords =
	[
		("თბილისი", "Tbilisi"),
		("tbilisi", "Tbilisi"),
		("ბათუმი", "Batumi"),
		("batumi", "Batumi"),
		("ქუთაისი", "Kutaisi"),
		("kutaisi", "Kutaisi"),
		("რუსთავი", "Rustavi"),
		("rustavi", "Rustavi"),
		("გორი", "Gori"),
		("gori", "Gori"),
		("ზუგდიდი", "Zugdidi"),
		("zugdidi", "Zugdidi"),
		("ფოთი", "Poti"),
		("poti", "Poti"),
		("თელავი", "Telavi"),
		("telavi", "Telavi"),
	];

	// Currency symbol/code, optional amount with separators, optional range, optional period hint nearby
	private static readonly System.Text.RegularExpressions.Regex SalaryRegex = new(
		"""
		(?ix)
		(?:
			(?<currency>GEL|USD|EUR|₾|\$|€)\s*
			(?<min>\d{1,3}(?:[,\s]\d{3})*|\d+)
			(?:\s*(?:-|–|to)\s*(?<max>\d{1,3}(?:[,\s]\d{3})*|\d+))?
		|
			(?<min>\d{1,3}(?:[,\s]\d{3})*|\d+)
			(?:\s*(?:-|–|to)\s*(?<max>\d{1,3}(?:[,\s]\d{3})*|\d+))?
			\s*(?<currency>GEL|USD|EUR|₾|\$|€)
		)
		(?:\s*(?:per\s+)?(?<period>month|monthly|hour|hourly|year|yearly|წელიწადში|თვეში|საათში))?
		""",
		System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace);

	public EnrichmentResult Extract(string title, string? description, string? descriptionHtml = null)
	{
		var text = string.Join('\n',
			title ?? string.Empty,
			description ?? string.Empty,
			StripTags(descriptionHtml));

		var (salaryMin, salaryMax, currency, period) = ExtractSalary(text);
		return new EnrichmentResult(
			salaryMin,
			salaryMax,
			currency,
			period,
			FindFirst(text, CityKeywords),
			FindFirst(text, WorkModeKeywords),
			FindFirst(text, EmploymentTypeKeywords),
			FindFirst(text, SeniorityKeywords),
			FindFirst(text, LanguageKeywords));
	}

	private static (decimal? Min, decimal? Max, string? Currency, string? Period) ExtractSalary(string text)
	{
		var match = SalaryRegex.Match(text);
		if (!match.Success)
			return (null, null, null, null);

		var min = ParseAmount(match.Groups["min"].Value);
		var max = match.Groups["max"].Success ? ParseAmount(match.Groups["max"].Value) : min;
		var currency = NormalizeCurrency(match.Groups["currency"].Value);
		var period = NormalizePeriod(match.Groups["period"].Success ? match.Groups["period"].Value : null);

		if (min is null)
			return (null, null, null, null);

		if (max is not null && max < min)
			(min, max) = (max, min);

		return (min, max, currency, period);
	}

	private static decimal? ParseAmount(string raw)
	{
		var cleaned = raw.Replace(",", "").Replace(" ", "");
		return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Number,
			System.Globalization.CultureInfo.InvariantCulture, out var value)
			? value
			: null;
	}

	private static string? NormalizeCurrency(string raw) => raw switch
	{
		"₾" or "GEL" or "gel" => "GEL",
		"$" or "USD" or "usd" => "USD",
		"€" or "EUR" or "eur" => "EUR",
		_ => raw.ToUpperInvariant()
	};

	private static string? NormalizePeriod(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return "month";

		var lower = raw.ToLowerInvariant();
		if (lower is "hour" or "hourly" or "საათში")
			return "hour";
		if (lower is "year" or "yearly" or "წელიწადში")
			return "year";
		return "month";
	}

	private static string? FindFirst(string text, (string Keyword, string Value)[] keywords)
	{
		foreach (var (keyword, value) in keywords)
		{
			if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
				return value;
		}

		return null;
	}

	private static string StripTags(string? html)
	{
		if (string.IsNullOrWhiteSpace(html))
			return string.Empty;

		var doc = new HtmlAgilityPack.HtmlDocument();
		doc.LoadHtml(html);
		return doc.DocumentNode.InnerText;
	}
}
