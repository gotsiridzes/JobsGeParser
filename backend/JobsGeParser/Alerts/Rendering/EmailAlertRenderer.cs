using System.Net;
using System.Text;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Alerts.Rendering;

/// <summary>
/// Text plus HTML email bodies. Every scraped value is HTML escaped: job titles and company names
/// come from third party HTML and go straight into a mail body, which is a real injection surface.
/// </summary>
public sealed class EmailAlertRenderer(IOptions<AlertOptions> options) : IAlertRenderer
{
	private readonly AlertEmailOptions _options = options.Value.Email;

	public AlertChannelKind Kind => AlertChannelKind.Email;

	public RenderedAlert RenderConfirmation(AlertConfirmationModel model)
	{
		var text = new StringBuilder()
			.AppendLine($"Confirm your job alert \"{model.SubscriptionName}\".")
			.AppendLine()
			.AppendLine(model.ConfirmUrl)
			.AppendLine()
			.AppendLine($"{model.CurrentlyMatchingJobs} jobs match this filter today. You will be alerted about new ones from now on.")
			.AppendLine()
			.AppendLine("If you did not request this, ignore this message and nothing further will be sent.")
			.ToString();

		var html = new StringBuilder()
			.Append("<p>Confirm your job alert <strong>").Append(Escape(model.SubscriptionName)).Append("</strong>.</p>")
			.Append("<p><a href=\"").Append(Escape(model.ConfirmUrl)).Append("\">Confirm this alert</a></p>")
			.Append("<p>").Append(model.CurrentlyMatchingJobs)
			.Append(" jobs match this filter today. You will be alerted about new ones from now on.</p>")
			.Append("<p>If you did not request this, ignore this message and nothing further will be sent.</p>")
			.ToString();

		return new RenderedAlert($"Confirm your job alert: {model.SubscriptionName}", text, html);
	}

	public RenderedAlert RenderInstantMatch(AlertMatchModel model)
	{
		var text = new StringBuilder()
			.AppendLine($"New job matching \"{model.SubscriptionName}\":")
			.AppendLine()
			.Append(TextJob(model.Job))
			.AppendLine()
			.AppendLine(Footer(model.ManageUrl, model.UnsubscribeUrl))
			.ToString();

		var html = new StringBuilder()
			.Append("<p>New job matching <strong>").Append(Escape(model.SubscriptionName)).Append("</strong>:</p>")
			.Append("<ul>").Append(HtmlJob(model.Job)).Append("</ul>")
			.Append(HtmlFooter(model.ManageUrl, model.UnsubscribeUrl))
			.ToString();

		return new RenderedAlert($"{model.Job.Title} at {model.Job.Company}", text, html);
	}

	public RenderedAlert RenderDigest(AlertDigestModel model)
	{
		var shown = model.Jobs.Take(_options.MaxDigestItems).ToList();
		var hidden = model.TotalMatches - shown.Count;

		var text = new StringBuilder();
		text.AppendLine($"{model.TotalMatches} new jobs matching \"{model.SubscriptionName}\":");
		text.AppendLine();

		foreach (var job in shown)
			text.Append(TextJob(job));

		if (hidden > 0)
		{
			text.AppendLine($"and {hidden} more: {model.ManageUrl}");
			text.AppendLine();
		}

		text.AppendLine(Footer(model.ManageUrl, model.UnsubscribeUrl));

		var html = new StringBuilder();
		html.Append("<p>").Append(model.TotalMatches).Append(" new jobs matching <strong>")
			.Append(Escape(model.SubscriptionName)).Append("</strong>:</p><ul>");

		foreach (var job in shown)
			html.Append(HtmlJob(job));

		html.Append("</ul>");

		if (hidden > 0)
		{
			html.Append("<p><a href=\"").Append(Escape(model.ManageUrl)).Append("\">and ")
				.Append(hidden).Append(" more</a></p>");
		}

		html.Append(HtmlFooter(model.ManageUrl, model.UnsubscribeUrl));

		var subject = model.TotalMatches == 1
			? $"1 new job: {model.SubscriptionName}"
			: $"{model.TotalMatches} new jobs: {model.SubscriptionName}";

		return new RenderedAlert(subject, text.ToString(), html.ToString());
	}

	private static string TextJob(AlertJobModel job)
	{
		var builder = new StringBuilder()
			.AppendLine($"{job.Title} at {job.Company}");

		var facts = AlertJobFormatting.Facts(job);
		if (facts.Length > 0)
			builder.AppendLine(facts);

		return builder
			.AppendLine(job.Link)
			.AppendLine()
			.ToString();
	}

	private static string HtmlJob(AlertJobModel job)
	{
		var builder = new StringBuilder()
			.Append("<li><a href=\"").Append(Escape(job.Link)).Append("\">")
			.Append(Escape(job.Title)).Append("</a> at ").Append(Escape(job.Company));

		var facts = AlertJobFormatting.Facts(job);
		if (facts.Length > 0)
			builder.Append("<br><small>").Append(Escape(facts)).Append("</small>");

		return builder.Append("</li>").ToString();
	}

	private static string Footer(string manageUrl, string unsubscribeUrl) =>
		$"Manage this alert: {manageUrl}{Environment.NewLine}Unsubscribe: {unsubscribeUrl}{Environment.NewLine}Job data from jobs.ge.";

	private static string HtmlFooter(string manageUrl, string unsubscribeUrl) =>
		new StringBuilder()
			.Append("<hr><p><small><a href=\"").Append(Escape(manageUrl)).Append("\">Manage this alert</a> | ")
			.Append("<a href=\"").Append(Escape(unsubscribeUrl)).Append("\">Unsubscribe</a><br>")
			.Append("Job data from <a href=\"https://jobs.ge/\">jobs.ge</a>.</small></p>")
			.ToString();

	private static string Escape(string value) => WebUtility.HtmlEncode(value);
}
