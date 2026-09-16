using JobsGeParser.Alerts;
using JobsGeParser.Configuration;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Workers;

/// <summary>
/// Thin timer around <see cref="AlertSendPump"/>. Deliberately holds no logic, so a test can
/// drive one pass of the pump directly.
/// </summary>
public class AlertSendWorker(
	IOptions<AlertOptions> options,
	AlertSendPump pump,
	ILogger<AlertSendWorker> logger) : BackgroundService
{
	private readonly AlertOptions _options = options.Value;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.Enabled)
		{
			logger.LogInformation("Alert send worker not started: alerts are disabled in configuration");
			return;
		}

		logger.LogInformation(
			"Alert send worker started: every {IntervalSeconds}s, batch {BatchSize}",
			_options.SendIntervalSeconds,
			_options.SendBatchSize);

		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.SendIntervalSeconds));

		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			try
			{
				await pump.PumpOnceAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				// One bad pass must not kill the worker for the life of the process.
				logger.LogError(ex, "Alert send pass failed");
			}
		}
	}
}
