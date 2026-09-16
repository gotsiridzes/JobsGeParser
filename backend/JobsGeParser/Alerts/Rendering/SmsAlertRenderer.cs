using System.Text;
using JobsGeParser.Configuration;
using JobsGeParser.Data;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Alerts.Rendering;

/// <summary>
/// One short body per message. The length cap lives here rather than in the channel so it is
/// enforced before anything is persisted, and so it is unit testable without a provider.
/// </summary>
public sealed class SmsAlertRenderer(IOptions<AlertOptions> options) : IAlertRenderer
{
	private readonly AlertSmsOptions _options = options.Value.Sms;

	public AlertChannelKind Kind => AlertChannelKind.Sms;

	public RenderedAlert RenderConfirmation(AlertConfirmationModel model)
	{
		var body = $"Confirm your jobs.ge alert \"{model.SubscriptionName}\": {model.ConfirmUrl}";

		return new RenderedAlert(null, Cap(body, model.ConfirmUrl));
	}

	public RenderedAlert RenderInstantMatch(AlertMatchModel model)
	{
		var body = new StringBuilder()
			.Append(model.Job.Title)
			.Append(" at ")
			.Append(model.Job.Company);

		var facts = AlertJobFormatting.Facts(model.Job, ", ");
		if (facts.Length > 0)
			body.Append(" (").Append(facts).Append(')');

		body.Append(' ').Append(model.Job.Link);

		return new RenderedAlert(null, Cap(body.ToString(), model.Job.Link));
	}

	public RenderedAlert RenderDigest(AlertDigestModel model)
	{
		var shown = model.Jobs.Take(_options.MaxDigestItems).ToList();
		var hidden = model.TotalMatches - shown.Count;

		var body = new StringBuilder()
			.Append(model.TotalMatches)
			.Append(model.TotalMatches == 1 ? " new job: " : " new jobs: ");

		body.Append(string.Join("; ", shown.Select(j => $"{j.Title} at {j.Company}")));

		if (hidden > 0)
			body.Append(" +").Append(hidden).Append(" more");

		body.Append(' ').Append(model.ManageUrl);

		return new RenderedAlert(null, Cap(body.ToString(), model.ManageUrl));
	}

	/// <summary>
	/// Truncates the prose while keeping the link intact. A cut link is worse than a cut sentence,
	/// so the link is appended after trimming rather than being trimmed with the rest.
	/// </summary>
	private string Cap(string body, string keepUrl)
	{
		if (body.Length <= _options.MaxLength)
			return body;

		var suffix = $" {keepUrl}";
		var room = _options.MaxLength - suffix.Length;

		if (room <= 0)
			return AlertJobFormatting.Truncate(body, _options.MaxLength);

		var prose = body.EndsWith(suffix, StringComparison.Ordinal)
			? body[..^suffix.Length]
			: body;

		return AlertJobFormatting.Truncate(prose, room) + suffix;
	}
}
