using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Options;
using Microsoft.Extensions.Options;

namespace API_Aggregation.Application.Statistics;

public sealed record PerformanceAnomaly(
    string ApiName,
    double RecentAverageMs,
    double OverallAverageMs,
    double DeviationPercent,
    long RecentRequestCount);

/// <summary>
/// Detects APIs whose average response time over the recent window (default 5 minutes)
/// is more than <see cref="AnomalyDetectionOptions.ThresholdPercent"/> above their overall average.
/// </summary>
public sealed class PerformanceAnomalyDetector(IApiStatisticsStore statistics, IOptionsMonitor<AnomalyDetectionOptions> optionsMonitor)
{
    public IReadOnlyList<PerformanceAnomaly> Detect()
    {
        var options = optionsMonitor.CurrentValue;
        var window = TimeSpan.FromMinutes(options.WindowMinutes);
        var anomalies = new List<PerformanceAnomaly>();

        foreach (var summary in statistics.GetPerformanceSummaries(window))
        {
            if (summary.RecentRequestCount < options.MinimumSamples || summary.OverallAverageMs <= 0)
                continue;

            var deviationPercent = (summary.RecentAverageMs - summary.OverallAverageMs) / summary.OverallAverageMs * 100;

            if (deviationPercent > options.ThresholdPercent)
            {
                anomalies.Add(new PerformanceAnomaly(
                    summary.ApiName,
                    Math.Round(summary.RecentAverageMs, 2),
                    Math.Round(summary.OverallAverageMs, 2),
                    Math.Round(deviationPercent, 1),
                    summary.RecentRequestCount));
            }
        }

        return anomalies;
    }
}
