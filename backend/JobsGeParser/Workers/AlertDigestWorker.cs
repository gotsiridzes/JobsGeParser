using JobsGeParser.Alerts;
using JobsGeParser.Configuration;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Workers;

/// <summary>
/// Thin timer around <see cref="AlertDigestBuilder"/>. Scans often enough that a send time is hit
/// within a few minutes, in any subscriber timezone.
/// </summary>
public class AlertDigestWorker(
	IOptions<AlertOptions> options,
	AlertDigestBuilder builder,
	ILogger<AlertDigestWorker> logger) : BackgroundService
{
	private readonly AlertOptions _options = options.Value;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.Enabled)
		{
			logger.LogInformation("Alert digest worker not started: alerts are disabled in configuration");
			return;
		}

		logger.LogInformation(
			"Alert digest worker started: scanning every {IntervalMinutes} min",
			_options.DigestScanIntervalMinutes);

		using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.DigestScanIntervalMinutes));

		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			try
			{
				await builder.BuildDueDigestsAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Alert digest pass failed");
			}
		}
	}
}
