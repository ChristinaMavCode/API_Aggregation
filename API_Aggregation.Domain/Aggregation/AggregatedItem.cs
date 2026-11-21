namespace API_Aggregation.Domain.Aggregation;

/// <summary>
/// Unified representation of a single piece of data coming from any external API.
/// Every external API client normalises its own payload into this shape so that
/// items from different sources can be filtered, sorted and paged together.
/// </summary>
public sealed class AggregatedItem
{
    /// <summary>Name of the external API that produced the item (e.g. "OpenWeatherMap").</summary>
    public required string Source { get; init; }

    public required ItemCategory Category { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    /// <summary>The point in time the item refers to (forecast time, publish date, last update...).</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Normalised relevance score in the range [0, 1]; higher is more relevant.</summary>
    public double Relevance { get; init; }

    public string? Url { get; init; }

    /// <summary>Source specific key/value details (temperature, stars, comments, ...).</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
