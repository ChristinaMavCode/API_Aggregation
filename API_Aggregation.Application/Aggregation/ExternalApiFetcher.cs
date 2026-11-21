using System.Collections.Concurrent;
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
///   <item>in-flight de-duplication (concurrent identical requests share one external call),</item>
///   <item>statistics recording (only real external calls are measured),</item>
///   <item>a fallback: if the API fails (after the HTTP resilience pipeline exhausted its retries)
///         the last known good response (stale cache) is returned, otherwise an empty, "Unavailable" result.</item>
/// </list>
/// Registered as a singleton; it is thread-safe.
/// </summary>
public sealed class ExternalApiFetcher(
    IMemoryCache cache,
    IApiStatisticsStore statistics,
    IOptionsMonitor<ExternalApiOptions> optionsMonitor,
    TimeProvider timeProvider,
    ILogger<ExternalApiFetcher> logger) : IExternalApiFetcher
{
    private const string UnavailableMessage = "The external API is currently unavailable.";

    private readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<AggregatedItem>>>> _inFlight = new();

    public async Task<SourceResult> FetchAsync(IExternalApiClient client, ExternalApiRequest request, CancellationToken cancellationToken)
    {
        var options = optionsMonitor.Get(client.Name);

        if (!options.Enabled)
            return new SourceResult(client.Name, SourceStatus.Disabled, [], Error: "The API is disabled by configuration.");

        if (client.RequiresApiKey && string.IsNullOrWhiteSpace(options.ApiKey))
            return new SourceResult(client.Name, SourceStatus.Disabled, [], Error: "The API key is not configured.");

        var cacheKey = $"{client.Name}:{client.GetCacheKey(request)}".ToLowerInvariant();

        if (cache.TryGetValue(FreshKey(cacheKey), out IReadOnlyList<AggregatedItem>? cachedItems) && cachedItems is not null)
            return new SourceResult(client.Name, SourceStatus.Cached, cachedItems, ResponseTimeMs: 0);

        var start = timeProvider.GetTimestamp();
        try
        {
            var items = await GetOrStartFetch(client, request, cacheKey, options).WaitAsync(cancellationToken);
            return new SourceResult(client.Name, SourceStatus.Live, items, ElapsedMs(start));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up (e.g. client disconnected) - propagate, this is not an API failure.
            throw;
        }
        catch (Exception ex)
        {
            if (cache.TryGetValue(StaleKey(cacheKey), out IReadOnlyList<AggregatedItem>? staleItems) && staleItems is not null)
            {
                logger.LogWarning(ex, "External API {ApiName} failed; serving {Count} stale items as fallback", client.Name, staleItems.Count);
                return new SourceResult(client.Name, SourceStatus.Stale, staleItems, ElapsedMs(start), UnavailableMessage + " Showing the last known data.");
            }

            logger.LogError(ex, "External API {ApiName} failed and no fallback data is available", client.Name);
            return new SourceResult(client.Name, SourceStatus.Unavailable, [], ElapsedMs(start), UnavailableMessage);
        }
    }

    /// <summary>
    /// Returns the in-flight task for the key or starts a new one. The external call itself is not bound to
    /// the caller's cancellation token, since other callers may be awaiting the same task; its duration
    /// is bounded by the HTTP resilience pipeline's total timeout instead.
    /// </summary>
    private Task<IReadOnlyList<AggregatedItem>> GetOrStartFetch(IExternalApiClient client, ExternalApiRequest request, string cacheKey, ExternalApiOptions options)
    {
        var candidate = new Lazy<Task<IReadOnlyList<AggregatedItem>>>(
            () => FetchAndCacheAsync(client, request, cacheKey, options),
            LazyThreadSafetyMode.ExecutionAndPublication);

        var lazy = _inFlight.GetOrAdd(cacheKey, candidate);

        if (ReferenceEquals(lazy, candidate))
        {
            // We own this fetch: remove it from the in-flight map once it completes (successfully or not).
            lazy.Value.ContinueWith(
                _ => _inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<IReadOnlyList<AggregatedItem>>>>(cacheKey, candidate)),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        return lazy.Value;
    }

    private async Task<IReadOnlyList<AggregatedItem>> FetchAndCacheAsync(IExternalApiClient client, ExternalApiRequest request, string cacheKey, ExternalApiOptions options)
    {
        var start = timeProvider.GetTimestamp();
        try
        {
            var items = await client.FetchAsync(request, CancellationToken.None);
            statistics.Record(client.Name, timeProvider.GetElapsedTime(start), success: true);

            cache.Set(FreshKey(cacheKey), items, options.CacheDuration);
            cache.Set(StaleKey(cacheKey), items, options.StaleCacheDuration);
            return items;
        }
        catch
        {
            statistics.Record(client.Name, timeProvider.GetElapsedTime(start), success: false);
            throw;
        }
    }

    private double ElapsedMs(long start) => Math.Round(timeProvider.GetElapsedTime(start).TotalMilliseconds, 2);

    private static string FreshKey(string key) => $"aggregation:fresh:{key}";

    private static string StaleKey(string key) => $"aggregation:stale:{key}";
}
