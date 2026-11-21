namespace API_Aggregation.Domain.Aggregation;

/// <summary>
/// Describes how the data of a single external API was obtained for a given aggregation request.
/// </summary>
public enum SourceStatus
{
    /// <summary>Data was fetched from the external API during this request.</summary>
    Live,

    /// <summary>Data was served from the (fresh) cache; no external call was made.</summary>
    Cached,

    /// <summary>The external API failed; the last known good (stale) data was returned as a fallback.</summary>
    Stale,

    /// <summary>The external API failed and no fallback data was available.</summary>
    Unavailable,

    /// <summary>The external API is disabled through configuration (e.g. missing API key).</summary>
    Disabled
}
