using API_Aggregation.Application.Options;
using API_Aggregation.Application.Statistics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API_Aggregation.Infrastructure.BackgroundServices;

/// <summary>
/// Periodically analyses the request statistics and logs a warning for every external API whose
/// average response time over the recent window is significantly above its overall average.
/// </summary>
public sealed class PerformanceAnomalyBackgroundService(
    PerformanceAnomalyDetector detector,
    IOptionsMonitor<AnomalyDetectionOptions> optionsMonitor,
    TimeProvider timeProvider,
    ILogger<PerformanceAnomalyBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = optionsMonitor.CurrentValue;
        if (!options.Enabled)
        {
            logger.LogInformation("Performance anomaly detection is disabled");
            return;
        }

        logger.LogInformation(
            "Performance anomaly detection started: every {IntervalSeconds}s, window {WindowMinutes}min, threshold {ThresholdPercent}%",
            options.IntervalSeconds, options.WindowMinutes, options.ThresholdPercent);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.IntervalSeconds)), timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                Analyze();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    /// <summary>Runs one analysis cycle. Exceptions are logged so that one failure never stops the service.</summary>
    internal void Analyze()
    {
        try
        {
            foreach (var anomaly in detector.Detect())
            {
                logger.LogWarning(
                    "Performance anomaly detected for {ApiName}: average over the last {WindowMinutes} min is {RecentAverageMs} ms " +
                    "({RecentRequestCount} requests) vs overall average {OverallAverageMs} ms (+{DeviationPercent}%)",
                    anomaly.ApiName, optionsMonitor.CurrentValue.WindowMinutes, anomaly.RecentAverageMs,
                    anomaly.RecentRequestCount, anomaly.OverallAverageMs, anomaly.DeviationPercent);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Performance anomaly analysis failed");
        }
    }
}
