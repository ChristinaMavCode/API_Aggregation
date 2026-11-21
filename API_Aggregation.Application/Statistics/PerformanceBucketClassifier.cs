using API_Aggregation.Application.Options;
using API_Aggregation.Domain.Statistics;

namespace API_Aggregation.Application.Statistics;

public static class PerformanceBucketClassifier
{
    /// <summary>
    /// Fast: &lt; FastThreshold, Average: FastThreshold..SlowThreshold (inclusive), Slow: &gt; SlowThreshold.
    /// </summary>
    public static PerformanceBucket Classify(double elapsedMs, StatisticsOptions options)
    {
        return elapsedMs < options.FastThresholdMs ? PerformanceBucket.Fast : 
             (elapsedMs <= options.SlowThresholdMs ? PerformanceBucket.Average : 
                                                     PerformanceBucket.Slow);
    }

    public static (double? Lower, double? Upper) GetBounds(PerformanceBucket bucket, StatisticsOptions options)
    {
        switch(bucket)
        {
            case PerformanceBucket.Fast:
                return (null, options.FastThresholdMs);
            case PerformanceBucket.Average:
                return (options.FastThresholdMs, options.SlowThresholdMs);
            default:
                return (options.SlowThresholdMs, null);
        }
    }
}
