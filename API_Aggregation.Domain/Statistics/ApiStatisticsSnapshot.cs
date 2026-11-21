namespace API_Aggregation.Domain.Statistics;

/// <summary>
/// Point-in-time, immutable view of the request statistics of one external API.
/// </summary>
public sealed record ApiStatisticsSnapshot(
    string ApiName,
    long TotalRequests,
    long FailedRequests,
    double AverageResponseTimeMs,
    IReadOnlyList<BucketStatistics> Buckets);

/// <summary>
/// Statistics of the requests that fell into a single performance bucket.
/// </summary>
/// <param name="LowerBoundMs">Inclusive lower bound in milliseconds (null = no lower bound).</param>
/// <param name="UpperBoundMs">Upper bound in milliseconds (null = no upper bound).</param>
public sealed record BucketStatistics(
    PerformanceBucket Bucket,
    double? LowerBoundMs,
    double? UpperBoundMs,
    long RequestCount,
    double AverageResponseTimeMs);

/// <summary>
/// Compares the recent performance of an API against its overall performance.
/// Used by the anomaly detection.
/// </summary>
public sealed record ApiPerformanceSummary(
    string ApiName,
    long OverallRequestCount,
    double OverallAverageMs,
    long RecentRequestCount,
    double RecentAverageMs);
