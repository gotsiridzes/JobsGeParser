using JobsGeParser.Alerts.Rendering;
using JobsGeParser.Configuration;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Tests;

public class AlertRendererTests
{
	private static readonly AlertOptions Options = new();

	private static readonly EmailAlertRenderer Email = new(Microsoft.Extensions.Options.Options.Create(Options));
	private static readonly SmsAlertRenderer Sms = new(Microsoft.Extensions.Options.Options.Create(Options));

	private static AlertJobModel Job(string title = "Senior .NET Developer", string company = "Acme LLC") =>
		new(1, title, company, "https://jobs.ge/en/?view=jobs&id=1", "Tbilisi", "remote", "senior", 3000, 5000, "GEL");

	private static AlertDigestModel Digest(int jobCount) =>
		new(
			"My alert",
			[.. Enumerable.Range(1, jobCount).Select(i => Job($"Job {i}"))],
			jobCount,
			"https://example.com/a/tok",
			"https://example.com/a/u/tok");

	[Fact]
	public void Email_EscapesScrapedValues()
	{
		var job = Job(title: "<script>alert(1)</script> Developer");
		var model = new AlertMatchModel("My alert", job, "https://example.com/a/t", "https://example.com/a/u/t");

		var rendered = Email.RenderInstantMatch(model);

		Assert.DoesNotContain("<script>", rendered.BodyHtml!, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("&lt;script&gt;", rendered.BodyHtml!, StringComparison.Ordinal);
	}

	[Fact]
	public void Email_InstantMatch_LinksBackToTheSourceAndCarriesUnsubscribe()
	{
		var model = new AlertMatchModel("My alert", Job(), "https://example.com/a/t", "https://example.com/a/u/t");

		var rendered = Email.RenderInstantMatch(model);

		Assert.Contains("jobs.ge", rendered.BodyText, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("https://example.com/a/u/t", rendered.BodyText, StringComparison.Ordinal);
		Assert.Contains("https://example.com/a/u/t", rendered.BodyHtml!, StringComparison.Ordinal);
	}

	[Fact]
	public void Email_Digest_CapsItemsAndSaysHowManyMore()
	{
		var rendered = Email.RenderDigest(Digest(Options.Email.MaxDigestItems + 7));

		Assert.Contains("and 7 more", rendered.BodyText, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(Options.Email.MaxDigestItems, CountOccurrences(rendered.BodyHtml!, "<li>"));
	}

	[Fact]
	public void Email_Digest_SubjectIsSingularForOneJob()
	{
		var rendered = Email.RenderDigest(Digest(1));

		Assert.Contains("1 new job:", rendered.Subject!, StringComparison.Ordinal);
	}

	[Fact]
	public void Sms_InstantMatch_NeverExceedsTheCap()
	{
		var job = Job(title: new string('ა', 500), company: new string('ბ', 200));
		var model = new AlertMatchModel("My alert", job, "https://example.com/a/t", "https://example.com/a/u/t");

		var rendered = Sms.RenderInstantMatch(model);

		Assert.True(rendered.BodyText.Length <= Options.Sms.MaxLength, $"Body was {rendered.BodyText.Length} characters.");
	}

	[Fact]
	public void Sms_InstantMatch_KeepsTheLinkIntactWhenTruncating()
	{
		var job = Job(title: new string('a', 500));
		var model = new AlertMatchModel("My alert", job, "https://example.com/a/t", "https://example.com/a/u/t");

		var rendered = Sms.RenderInstantMatch(model);

		Assert.EndsWith(job.Link, rendered.BodyText, StringComparison.Ordinal);
	}

	[Fact]
	public void Sms_Digest_CapsItemCountAndLength()
	{
		var rendered = Sms.RenderDigest(Digest(50));

		Assert.True(rendered.BodyText.Length <= Options.Sms.MaxLength);
		Assert.Contains("more", rendered.BodyText, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Sms_Confirmation_AlwaysCarriesTheConfirmLink()
	{
		var rendered = Sms.RenderConfirmation(
			new AlertConfirmationModel("https://example.com/a/c/tok", new string('x', 400), 12));

		Assert.EndsWith("https://example.com/a/c/tok", rendered.BodyText, StringComparison.Ordinal);
		Assert.True(rendered.BodyText.Length <= Options.Sms.MaxLength);
	}

	[Fact]
	public void Sms_HasNoSubject()
	{
		Assert.Null(Sms.RenderDigest(Digest(2)).Subject);
	}

	[Fact]
	public void Formatting_SalaryRange_IsReadable()
	{
		Assert.Equal("3,000 to 5,000 GEL", AlertJobFormatting.Salary(Job()));
	}

	[Fact]
	public void Formatting_SkipsFactsTheExtractorCouldNotDetermine()
	{
		var job = new AlertJobModel(1, "Dev", "Acme", "https://jobs.ge/1", null, null, null, null, null, null);

		Assert.Equal("", AlertJobFormatting.Facts(job));
		Assert.Null(AlertJobFormatting.Salary(job));
	}

	private static int CountOccurrences(string haystack, string needle)
	{
		var count = 0;
		var index = haystack.IndexOf(needle, StringComparison.Ordinal);

		while (index >= 0)
		{
			count++;
			index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
		}

		return count;
	}
}
