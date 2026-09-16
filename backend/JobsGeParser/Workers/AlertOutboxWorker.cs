using JobsGeParser.Alerts;
using JobsGeParser.Configuration;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Workers;

/// <summary>
/// Thin timer around <see cref="AlertOutboxDrainer"/>. This is the whole decoupling: the scrape
/// loop writes an outbox row and moves on, and everything alert related happens here instead.
/// </summary>
public class AlertOutboxWorker(
	IOptions<AlertOptions> options,
	AlertOutboxDrainer drainer,
	ILogger<AlertOutboxWorker> logger) : BackgroundService
{
	private readonly AlertOptions _options = options.Value;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.Enabled)
		{
			logger.LogInformation("Alert outbox worker not started: alerts are disabled in configuration");
			return;
		}

		logger.LogInformation(
			"Alert outbox worker started: every {IntervalSeconds}s, batch {BatchSize}, settle delay {SettleSeconds}s",
			_options.OutboxDrainIntervalSeconds,
			_options.OutboxBatchSize,
			_options.EventSettleDelaySeconds);

		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.OutboxDrainIntervalSeconds));

		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			try
			{
				// Keep draining while batches come back full, so a scrape burst is not spread
				// across many ticks.
				int processed;
				do
				{
					processed = await drainer.DrainOnceAsync(stoppingToken);
				}
				while (processed >= _options.OutboxBatchSize && !stoppingToken.IsCancellationRequested);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Alert outbox drain failed");
			}
		}
	}
}
