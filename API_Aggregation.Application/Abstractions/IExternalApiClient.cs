using API_Aggregation.Domain.Aggregation;

namespace API_Aggregation.Application.Abstractions;

/// <summary>
/// Contract that every external API integration implements.
/// <para>
/// To integrate a new API: implement this interface (normalising the payload into
/// <see cref="AggregatedItem"/>), register it with <c>AddExternalApiClient&lt;T&gt;()</c> and
/// add a configuration section under <c>ExternalApis:{Name}</c>. Caching, resilience,
/// fallback, statistics, filtering and sorting are then applied automatically.
/// </para>
/// </summary>
public interface IExternalApiClient
{
    /// <summary>Unique name of the API; also used as the configuration section and statistics key.</summary>
    string Name { get; }

    ItemCategory Category { get; }

    /// <summary>
    /// Builds the part of the cache key that identifies the request for this client.
    /// Clients that ignore some request parameters override it so that they don't
    /// produce redundant cache entries (and therefore redundant external calls).
    /// </summary>
    string GetCacheKey(ExternalApiRequest request) => $"{request.City}|{request.Topic}";

    /// <summary>Fetches and normalises data. Implementations throw on failure; fallback is handled by the caller.</summary>
    Task<IReadOnlyList<AggregatedItem>> FetchAsync(ExternalApiRequest request, CancellationToken cancellationToken);
}
