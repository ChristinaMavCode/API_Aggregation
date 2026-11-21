using System.Globalization;
using System.Text.Json.Serialization;
using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Options;
using API_Aggregation.Domain.Aggregation;
using Microsoft.Extensions.Options;

namespace API_Aggregation.Infrastructure.ExternalApis;

/// <summary>
/// Hacker News stories through the Algolia search API (https://hn.algolia.com/api). No API key required.
/// </summary>
public sealed class HackerNewsClient : ExternalApiClientBase
{
    public const string ApiName = "HackerNews";

    public HackerNewsClient(HttpClient httpClient, IOptionsMonitor<ExternalApiOptions> optionsMonitor)
        : base(httpClient, optionsMonitor)
    {
    }

    public override string Name => ApiName;

    public override ItemCategory Category => ItemCategory.News;

    // News only depends on the topic.
    public override string GetCacheKey(ExternalApiRequest request) => request.Topic;

    public override async Task<IReadOnlyList<AggregatedItem>> FetchAsync(ExternalApiRequest request, CancellationToken cancellationToken)
    {
        var uri = $"api/v1/search?query={Uri.EscapeDataString(request.Topic)}&tags=story&hitsPerPage={Options.MaxItems}";
        var response = await GetJsonAsync<SearchResponse>(uri, cancellationToken);

        var hits = response?.Hits?.Where(h => !string.IsNullOrWhiteSpace(h.Title)).ToList();
        if (hits is null || hits.Count == 0)
            return [];

        // Algolia already returns the hits ordered by relevance.
        return hits
            .Select((hit, index) => new AggregatedItem
            {
                Source = Name,
                Category = Category,
                Title = hit.Title!,
                Description = $"{hit.Points ?? 0} points by {hit.Author} | {hit.NumComments ?? 0} comments",
                Timestamp = hit.CreatedAt,
                Relevance = RankRelevance(index, hits.Count),
                Url = hit.Url ?? $"https://news.ycombinator.com/item?id={hit.ObjectId}",
                Metadata = new Dictionary<string, string>
                {
                    ["author"] = hit.Author ?? string.Empty,
                    ["points"] = (hit.Points ?? 0).ToString(CultureInfo.InvariantCulture),
                    ["comments"] = (hit.NumComments ?? 0).ToString(CultureInfo.InvariantCulture),
                    ["discussionUrl"] = $"https://news.ycombinator.com/item?id={hit.ObjectId}"
                }
            })
            .ToList();
    }

    private sealed record SearchResponse(List<Hit>? Hits);

    private sealed record Hit(
        [property: JsonPropertyName("objectID")] string ObjectId,
        string? Title,
        string? Url,
        string? Author,
        int? Points,
        [property: JsonPropertyName("num_comments")] int? NumComments,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);
}
