using System.Net;
using System.Text;
using JobsGeParser.Alerts.Channels;
using JobsGeParser.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Tests;

/// <summary>
/// Failure classification is the whole job of a channel: calling a permanent failure transient
/// retries something that will never work, and calling a transient failure permanent loses an
/// alert and can suppress a perfectly good subscriber.
/// </summary>
public class PostmarkEmailChannelTests
{
	private static readonly OutboundAlertMessage Message = new(
		Guid.CreateVersion7(),
		"someone@example.com",
		"New job",
		"A job you might like.",
		"<p>A job you might like.</p>",
		"instant:1:2",
		"https://example.test/a/u/token");

	private static (PostmarkEmailChannel Channel, StubHandler Handler) Create(
		HttpStatusCode status,
		string body = "{}",
		string? retryAfterSeconds = null)
	{
		var handler = new StubHandler(status, body, retryAfterSeconds);

		var options = Options.Create(new AlertOptions
		{
			Email = new AlertEmailOptions
			{
				Provider = "Postmark",
				FromAddress = "alerts@example.test",
				FromName = "JobsGe Alerts",
				ApiKey = "test-key"
			}
		});

		var channel = new PostmarkEmailChannel(
			new StubHttpClientFactory(handler),
			options,
			NullLogger<PostmarkEmailChannel>.Instance);

		return (channel, handler);
	}

	[Fact]
	public async Task Success_ReturnsSentWithTheProviderMessageId()
	{
		var (channel, _) = Create(HttpStatusCode.OK, """{"MessageID":"abc-123","ErrorCode":0,"Message":"OK"}""");

		var result = await channel.SendAsync(Message, CancellationToken.None);

		Assert.Equal(AlertSendOutcome.Sent, result.Outcome);
		Assert.Equal("abc-123", result.ProviderMessageId);
	}

	[Fact]
	public async Task Request_CarriesOneClickUnsubscribeHeaders()
	{
		var (channel, handler) = Create(HttpStatusCode.OK, """{"MessageID":"abc"}""");

		await channel.SendAsync(Message, CancellationToken.None);

		Assert.Contains("List-Unsubscribe", handler.RequestBody, StringComparison.Ordinal);
		Assert.Contains("List-Unsubscribe=One-Click", handler.RequestBody, StringComparison.Ordinal);
		Assert.Contains("https://example.test/a/u/token", handler.RequestBody, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Request_UsesPascalCasePropertyNames()
	{
		var (channel, handler) = Create(HttpStatusCode.OK, """{"MessageID":"abc"}""");

		await channel.SendAsync(Message, CancellationToken.None);

		// Postmark rejects camelCase, which is what the JSON web defaults would produce.
		Assert.Contains("\"From\"", handler.RequestBody, StringComparison.Ordinal);
		Assert.Contains("\"To\"", handler.RequestBody, StringComparison.Ordinal);
		Assert.Contains("\"TextBody\"", handler.RequestBody, StringComparison.Ordinal);
		Assert.Contains("\"HtmlBody\"", handler.RequestBody, StringComparison.Ordinal);
		Assert.Contains("\"MessageStream\"", handler.RequestBody, StringComparison.Ordinal);
		Assert.DoesNotContain("\"from\"", handler.RequestBody, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Request_SendsTheConfiguredSender()
	{
		var (channel, handler) = Create(HttpStatusCode.OK, """{"MessageID":"abc"}""");

		await channel.SendAsync(Message, CancellationToken.None);

		Assert.Contains("JobsGe Alerts", handler.RequestBody, StringComparison.Ordinal);
		Assert.Contains("alerts@example.test", handler.RequestBody, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(HttpStatusCode.TooManyRequests)]
	[InlineData(HttpStatusCode.InternalServerError)]
	[InlineData(HttpStatusCode.BadGateway)]
	[InlineData(HttpStatusCode.ServiceUnavailable)]
	[InlineData(HttpStatusCode.GatewayTimeout)]
	[InlineData(HttpStatusCode.RequestTimeout)]
	public async Task TransientStatuses_AreRetried(HttpStatusCode status)
	{
		var (channel, _) = Create(status);

		var result = await channel.SendAsync(Message, CancellationToken.None);

		Assert.Equal(AlertSendOutcome.TransientFailure, result.Outcome);
		Assert.False(result.SuppressSubscriber);
	}

	[Fact]
	public async Task RetryAfterHeader_IsPassedThrough()
	{
		var (channel, _) = Create(HttpStatusCode.TooManyRequests, retryAfterSeconds: "120");

		var result = await channel.SendAsync(Message, CancellationToken.None);

		Assert.Equal(TimeSpan.FromSeconds(120), result.RetryAfter);
	}

	[Fact]
	public async Task InactiveRecipient_SuppressesTheSubscriber()
	{
		var (channel, _) = Create(
			HttpStatusCode.UnprocessableEntity,
			"""{"ErrorCode":406,"Message":"You tried to send to a recipient that has been marked as inactive."}""");

		var result = await channel.SendAsync(Message, CancellationToken.None);

		Assert.Equal(AlertSendOutcome.PermanentFailure, result.Outcome);
		Assert.True(result.SuppressSubscriber);
	}

	[Fact]
	public async Task InvalidEmail_SuppressesTheSubscriber()
	{
		var (channel, _) = Create(
			HttpStatusCode.UnprocessableEntity,
			"""{"ErrorCode":300,"Message":"Invalid email request."}""");

		var result = await channel.SendAsync(Message, CancellationToken.None);

		Assert.True(result.SuppressSubscriber);
	}

	[Fact]
	public async Task BadCredentials_FailPermanentlyWithoutBlamingTheSubscriber()
	{
		var (channel, _) = Create(HttpStatusCode.Unauthorized, """{"ErrorCode":10,"Message":"Bad or missing token."}""");

		var result = await channel.SendAsync(Message, CancellationToken.None);

		Assert.Equal(AlertSendOutcome.PermanentFailure, result.Outcome);
		Assert.False(result.SuppressSubscriber);
	}

	[Fact]
	public async Task NonJsonErrorBody_DoesNotThrow()
	{
		var (channel, _) = Create(HttpStatusCode.BadRequest, "<html>gateway error</html>");

		var result = await channel.SendAsync(Message, CancellationToken.None);

		Assert.Equal(AlertSendOutcome.PermanentFailure, result.Outcome);
	}

	[Fact]
	public async Task NetworkFailure_IsTransient()
	{
		var handler = new StubHandler(HttpStatusCode.OK, "{}") { Throw = new HttpRequestException("connection reset") };

		var channel = new PostmarkEmailChannel(
			new StubHttpClientFactory(handler),
			Options.Create(new AlertOptions()),
			NullLogger<PostmarkEmailChannel>.Instance);

		var result = await channel.SendAsync(Message, CancellationToken.None);

		Assert.Equal(AlertSendOutcome.TransientFailure, result.Outcome);
	}

	private sealed class StubHandler(HttpStatusCode status, string body, string? retryAfterSeconds = null)
		: HttpMessageHandler
	{
		public string RequestBody { get; private set; } = "";

		public Exception? Throw { get; init; }

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			if (Throw is not null)
				throw Throw;

			if (request.Content is not null)
				RequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

			var response = new HttpResponseMessage(status)
			{
				Content = new StringContent(body, Encoding.UTF8, "application/json")
			};

			if (retryAfterSeconds is not null)
				response.Headers.TryAddWithoutValidation("Retry-After", retryAfterSeconds);

			return response;
		}
	}

	private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
	{
		public HttpClient CreateClient(string name) =>
			new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.postmarkapp.test/") };
	}
}
