using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Options;
using API_Aggregation.Domain.Aggregation;
using Microsoft.Extensions.Options;

namespace API_Aggregation.Infrastructure.ExternalApis;

/// <summary>
/// Common plumbing for the typed HTTP clients. The <see cref="HttpClient"/> is created by
/// <see cref="IHttpClientFactory"/> and already contains the resilience pipeline
/// (retries with exponential back-off + jitter, timeouts and circuit breaker).
/// </summary>
public abstract class ExternalApiClientBase(HttpClient httpClient, IOptionsMonitor<ExternalApiOptions> optionsMonitor)
    : IExternalApiClient
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected HttpClient HttpClient { get; } = httpClient;

    protected ExternalApiOptions Options => optionsMonitor.Get(Name);

    public abstract string Name { get; }

    public abstract ItemCategory Category { get; }

    // Declared virtual here (instead of relying on the interface default members) so that
    // derived clients can override them: interface mapping is resolved on this base class.
    public virtual bool RequiresApiKey => false;

    public virtual string GetCacheKey(ExternalApiRequest request) => $"{request.City}|{request.Topic}";

    public abstract Task<IReadOnlyList<AggregatedItem>> FetchAsync(ExternalApiRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// GETs and deserialises JSON. Returns <c>null</c> for 404 (e.g. unknown city), which is a valid
    /// "no data" answer rather than an outage. Any other non-success status code throws.
    /// </summary>
    protected async Task<T?> GetJsonAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    /// <summary>Relevance of a time based item: 1 for "now", decreasing the further away it is.</summary>
    protected static double TimeProximityRelevance(DateTimeOffset timestamp, DateTimeOffset now)
    {
        var hours = Math.Abs((timestamp - now).TotalHours);
        return Math.Round(1d / (1d + hours / 24d), 4);
    }

    /// <summary>Relevance derived from the position in a list already ranked by the provider.</summary>
    protected static double RankRelevance(int index, int count) =>
        count == 0 ? 0 : Math.Round((count - index) / (double)count, 4);
}
