namespace API_Aggregation.Domain.Aggregation;

/// <summary>
/// Outcome of querying a single external API as part of an aggregation request.
/// </summary>
public sealed record SourceResult(
    string Source,
    SourceStatus Status,
    IReadOnlyList<AggregatedItem> Items,
    double? ResponseTimeMs = null,
    string? Error = null);
