using JobsGeParser.Data;

namespace JobsGeParser.Alerts.Rendering;

/// <summary>
/// Turns a model into a message body. Pure and synchronous on purpose: the output is persisted at
/// enqueue, so a retry can never re-render into something different.
/// </summary>
public interface IAlertRenderer
{
	AlertChannelKind Kind { get; }

	RenderedAlert RenderConfirmation(AlertConfirmationModel model);

	RenderedAlert RenderInstantMatch(AlertMatchModel model);

	RenderedAlert RenderDigest(AlertDigestModel model);
}

public sealed record RenderedAlert(string? Subject, string BodyText, string? BodyHtml = null);
