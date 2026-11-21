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
public sealed class PerformanceAnomalyBackgroundService : BackgroundService
{
    private readonly PerformanceAnomalyDetector _detector;
    private readonly IOptionsMonitor<AnomalyDetectionOptions> _optionsMonitor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PerformanceAnomalyBackgroundService> _logger;

    public PerformanceAnomalyBackgroundService(
        PerformanceAnomalyDetector detector,
        IOptionsMonitor<AnomalyDetectionOptions> optionsMonitor,
        TimeProvider timeProvider,
        ILogger<PerformanceAnomalyBackgroundService> logger)
    {
        _detector = detector;
        _optionsMonitor = optionsMonitor;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (!options.Enabled)
        {
            _logger.LogInformation("Performance anomaly detection is disabled");
            return;
        }

        _logger.LogInformation(
            "Performance anomaly detection started: every {IntervalSeconds}s, window {WindowMinutes}min, threshold {ThresholdPercent}%",
            options.IntervalSeconds, options.WindowMinutes, options.ThresholdPercent);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.IntervalSeconds)), _timeProvider);

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
            foreach (var anomaly in _detector.Detect())
            {
                _logger.LogWarning(
                    "Performance anomaly detected for {ApiName}: average over the last {WindowMinutes} min is {RecentAverageMs} ms " +
                    "({RecentRequestCount} requests) vs overall average {OverallAverageMs} ms (+{DeviationPercent}%)",
                    anomaly.ApiName, _optionsMonitor.CurrentValue.WindowMinutes, anomaly.RecentAverageMs,
                    anomaly.RecentRequestCount, anomaly.OverallAverageMs, anomaly.DeviationPercent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Performance anomaly analysis failed");
        }
    }
}
