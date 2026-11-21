namespace API_Aggregation.Application.Dtos;

/// <summary>Response of the statistics endpoint.</summary>
public sealed record ApiStatisticsResponseDto
{
    public required DateTimeOffset GeneratedAt { get; init; }

    public required double FastThresholdMs { get; init; }

    public required double SlowThresholdMs { get; init; }

    public required IReadOnlyList<ApiStatisticsDto> Apis { get; init; }
}

public sealed record ApiStatisticsDto
{
    public required string ApiName { get; init; }

    public long TotalRequests { get; init; }

    public long FailedRequests { get; init; }

    public double AverageResponseTimeMs { get; init; }

    public required IReadOnlyList<BucketStatisticsDto> Buckets { get; init; }
}

public sealed record BucketStatisticsDto
{
    /// <summary>Fast, Average or Slow.</summary>
    public required string Bucket { get; init; }

    public double? LowerBoundMs { get; init; }

    public double? UpperBoundMs { get; init; }

    public long RequestCount { get; init; }

    public double AverageResponseTimeMs { get; init; }
}
