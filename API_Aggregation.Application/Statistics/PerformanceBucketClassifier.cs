using API_Aggregation.Application.Options;
using API_Aggregation.Domain.Statistics;

namespace API_Aggregation.Application.Statistics;

public static class PerformanceBucketClassifier
{
    /// <summary>
    /// Fast: &lt; FastThreshold, Average: FastThreshold..SlowThreshold (inclusive), Slow: &gt; SlowThreshold.
    /// </summary>
    public static PerformanceBucket Classify(double elapsedMs, StatisticsOptions options) =>
        elapsedMs < options.FastThresholdMs ? PerformanceBucket.Fast
        : elapsedMs <= options.SlowThresholdMs ? PerformanceBucket.Average
        : PerformanceBucket.Slow;

    public static (double? Lower, double? Upper) GetBounds(PerformanceBucket bucket, StatisticsOptions options) => bucket switch
    {
        PerformanceBucket.Fast => (null, options.FastThresholdMs),
        PerformanceBucket.Average => (options.FastThresholdMs, options.SlowThresholdMs),
        _ => (options.SlowThresholdMs, null)
    };
}
