using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Options;
using API_Aggregation.Domain.Aggregation;
using Microsoft.Extensions.Options;

namespace API_Aggregation.Infrastructure.ExternalApis;

/// <summary>
/// GitHub repository search (https://docs.github.com/en/rest/search). Works without a token
/// (low rate limit); a personal access token can be configured as the API key.
/// </summary>
public sealed class GitHubClient : ExternalApiClientBase
{
    public const string ApiName = "GitHub";

    public GitHubClient(HttpClient httpClient, IOptionsMonitor<ExternalApiOptions> optionsMonitor)
        : base(httpClient, optionsMonitor)
    {
    }

    public override string Name => ApiName;

    public override ItemCategory Category => ItemCategory.Repository;

    public override string GetCacheKey(ExternalApiRequest request) => request.Topic;

    public override async Task<IReadOnlyList<AggregatedItem>> FetchAsync(ExternalApiRequest request, CancellationToken cancellationToken)
    {
        var options = Options;
        var uri = $"search/repositories?q={Uri.EscapeDataString(request.Topic)}&sort=stars&order=desc&per_page={options.MaxItems}";

        using var message = new HttpRequestMessage(HttpMethod.Get, uri);
        message.Headers.Accept.ParseAdd("application/vnd.github+json");
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        using var response = await HttpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SearchResponse>(JsonOptions, cancellationToken);
        var repositories = result?.Items;
        if (repositories is null || repositories.Count == 0)
            return [];

        var maxStars = Math.Max(1, repositories.Max(r => r.StargazersCount));

        return repositories
            .Select(repo => new AggregatedItem
            {
                Source = Name,
                Category = Category,
                Title = repo.FullName,
                Description = repo.Description,
                Timestamp = repo.PushedAt ?? repo.UpdatedAt,
                Relevance = Math.Round(repo.StargazersCount / (double)maxStars, 4),
                Url = repo.HtmlUrl,
                Metadata = new Dictionary<string, string>
                {
                    ["stars"] = repo.StargazersCount.ToString(CultureInfo.InvariantCulture),
                    ["forks"] = repo.ForksCount.ToString(CultureInfo.InvariantCulture),
                    ["language"] = repo.Language ?? string.Empty
                }
            })
            .ToList();
    }

    private sealed record SearchResponse(List<Repository>? Items);

    private sealed record Repository(
        [property: JsonPropertyName("full_name")] string FullName,
        string? Description,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("stargazers_count")] int StargazersCount,
        [property: JsonPropertyName("forks_count")] int ForksCount,
        string? Language,
        [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
        [property: JsonPropertyName("pushed_at")] DateTimeOffset? PushedAt);
}
