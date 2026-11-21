namespace API_Aggregation.Application.Dtos;

/// <summary>Response of the aggregation endpoint.</summary>
public sealed record AggregationResponseDto
{
    public required string City { get; init; }

    public required string Topic { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>Number of items that matched the filters (before paging).</summary>
    public required int TotalItems { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalPages { get; init; }

    /// <summary>Status of every external API that took part in the request.</summary>
    public required IReadOnlyList<SourceStatusDto> Sources { get; init; }

    public required IReadOnlyList<AggregatedItemDto> Items { get; init; }
}

public sealed record AggregatedItemDto
{
    public required string Source { get; init; }

    public required string Category { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public double Relevance { get; init; }

    public string? Url { get; init; }

    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
}

public sealed record SourceStatusDto
{
    public required string Source { get; init; }

    /// <summary>Live, Cached, Stale, Unavailable or Disabled.</summary>
    public required string Status { get; init; }

    public int ItemCount { get; init; }

    public double? ResponseTimeMs { get; init; }

    public string? Error { get; init; }
}
