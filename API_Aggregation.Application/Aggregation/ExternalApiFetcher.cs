using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Options;
using API_Aggregation.Domain.Aggregation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API_Aggregation.Application.Aggregation;

/// <summary>
/// Wraps every external API call with:
/// <list type="number">
///   <item>a fresh cache (avoids redundant calls within <see cref="ExternalApiOptions.CacheDuration"/>),</item>
///   <item>statistics recording (only real external calls are measured),</item>
///   <item>a fallback: if the API fails (after the HTTP resilience pipeline exhausted its retries)
///         the last known good response (stale cache) is returned, otherwise an empty, "Unavailable" result.</item>
/// </list>
/// The caller's cancellation token is passed to the external call, so if the caller disconnects
/// the pending HTTP request is cancelled too.
/// Registered as a singleton; it is thread-safe.
/// </summary>
public sealed class ExternalApiFetcher : IExternalApiFetcher
{
    private const string UnavailableMessage = "The external API is currently unavailable.";

    private readonly IMemoryCache _cache;
    private readonly IApiStatisticsStore _statistics;
    private readonly IOptionsMonitor<ExternalApiOptions> _optionsMonitor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ExternalApiFetcher> _logger;

    public ExternalApiFetcher(
        IMemoryCache cache,
        IApiStatisticsStore statistics,
        IOptionsMonitor<ExternalApiOptions> optionsMonitor,
        TimeProvider timeProvider,
        ILogger<ExternalApiFetcher> logger)
    {
        _cache = cache;
        _statistics = statistics;
        _optionsMonitor = optionsMonitor;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<SourceResult> FetchAsync(IExternalApiClient client, ExternalApiRequest request, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.Get(client.Name);

        if (!options.Enabled)
            return new SourceResult(client.Name, SourceStatus.Disabled, [], Error: "The API is disabled by configuration.");

        var cacheKey = $"{client.Name}:{client.GetCacheKey(request)}".ToLowerInvariant();

        if (_cache.TryGetValue(_getFreshKey(cacheKey), out IReadOnlyList<AggregatedItem>? cachedItems) && cachedItems is not null)
            return new SourceResult(client.Name, SourceStatus.Cached, cachedItems, ResponseTimeMs: 0);

        var start = _timeProvider.GetTimestamp();
        try
        {
            var items = await client.FetchAsync(request, cancellationToken);
            _statistics.Record(client.Name, _timeProvider.GetElapsedTime(start), success: true);

            _cache.Set(_getFreshKey(cacheKey), items, options.CacheDuration);
            _cache.Set(_getStaleKey(cacheKey), items, options.StaleCacheDuration);

            return new SourceResult(client.Name, SourceStatus.Live, items, _calcElapsedMs(start));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up (e.g. client disconnected) - propagate, this is not an API failure.
            throw;
        }
        catch (Exception ex)
        {
            _statistics.Record(client.Name, _timeProvider.GetElapsedTime(start), success: false);

            if (_cache.TryGetValue(_getStaleKey(cacheKey), out IReadOnlyList<AggregatedItem>? staleItems) && staleItems is not null)
            {
                _logger.LogWarning(ex, "External API {ApiName} failed; serving {Count} stale items as fallback", client.Name, staleItems.Count);
                return new SourceResult(client.Name, SourceStatus.Stale, staleItems, _calcElapsedMs(start), UnavailableMessage + " Showing the last known data.");
            }

            _logger.LogError(ex, "External API {ApiName} failed and no fallback data is available", client.Name);
            return new SourceResult(client.Name, SourceStatus.Unavailable, [], _calcElapsedMs(start), UnavailableMessage);
        }
    }

    private double _calcElapsedMs(long start)
    {
        return Math.Round(_timeProvider.GetElapsedTime(start).TotalMilliseconds, 2);
    }

    private static string _getFreshKey(string key)
    {
        return $"aggregation:fresh:{key}";
    }

    private static string _getStaleKey(string key)
    {
        return $"aggregation:stale:{key}";
    }
}
