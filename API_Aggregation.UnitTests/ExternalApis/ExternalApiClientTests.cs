using System.Net;
using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Options;
using API_Aggregation.Domain.Aggregation;
using API_Aggregation.Infrastructure.ExternalApis;
using API_Aggregation.UnitTests.TestHelpers;
using Microsoft.Extensions.Time.Testing;

namespace API_Aggregation.UnitTests.ExternalApis;

/// <summary>
/// Verifies that every client builds the right request and normalises the provider payload correctly.
/// HTTP is stubbed, so no network access is needed.
/// </summary>
public class ExternalApiClientTests
{
    private static readonly ExternalApiRequest Request = new ExternalApiRequest("Athens", "dotnet");
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private static HttpClient Http(HttpMessageHandler handler, string baseUrl) => new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };

    private static TestOptionsMonitor<ExternalApiOptions> Options(string name, ExternalApiOptions options) =>
        new TestOptionsMonitor<ExternalApiOptions>(new ExternalApiOptions()).With(name, options);

    [Fact]
    public async Task OpenMeteo_GeocodesTheCity_ThenMapsTheDailyForecast()
    {
        var handler = new StubHttpMessageHandler(req => req.RequestUri!.Host.StartsWith("geocoding")
            ? StubHttpMessageHandler.JsonResponse("""{ "results": [ { "name": "Athens", "latitude": 37.98, "longitude": 23.72, "country": "Greece" } ] }""")
            : StubHttpMessageHandler.JsonResponse("""
                {
                  "utc_offset_seconds": 7200,
                  "daily": {
                    "time": ["2026-11-21", "2026-11-22"],
                    "weather_code": [3, 61],
                    "temperature_2m_max": [19.5, 17.1],
                    "temperature_2m_min": [12.0, 11.2],
                    "precipitation_sum": [0, 4.2],
                    "wind_speed_10m_max": [10.1, 22.3]
                  }
                }
                """));
        var client = new OpenMeteoClient(
            Http(handler, "https://api.open-meteo.com/"),
            Options(OpenMeteoClient.ApiName, new ExternalApiOptions { MaxItems = 2 }),
            new FakeTimeProvider(Now));

        var items = await client.FetchAsync(Request, CancellationToken.None);

        Assert.Equal(2, items.Count);
        Assert.Equal("Athens: 11.2–17.1°C, rain", items[1].Title);
        Assert.Equal(new DateTimeOffset(2026, 11, 22, 0, 0, 0, TimeSpan.FromHours(2)), items[1].Timestamp);
        Assert.Equal("4.2", items[1].Metadata["precipitationMm"]);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("latitude=37.98", handler.Requests[1].ToString());
        Assert.Contains("forecast_days=2", handler.Requests[1].ToString());
    }

    [Fact]
    public async Task OpenMeteo_ReturnsNoItems_AndSkipsTheForecastCall_WhenTheCityIsNotFound()
    {
        var handler = StubHttpMessageHandler.Json("""{ "generationtime_ms": 0.1 }""");
        var client = new OpenMeteoClient(
            Http(handler, "https://api.open-meteo.com/"),
            Options(OpenMeteoClient.ApiName, new ExternalApiOptions()),
            new FakeTimeProvider(Now));

        Assert.Empty(await client.FetchAsync(Request, CancellationToken.None));
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(0, "clear sky")]
    [InlineData(3, "overcast")]
    [InlineData(63, "rain")]
    [InlineData(75, "snow")]
    [InlineData(95, "thunderstorm")]
    [InlineData(42, "unknown")]
    public void OpenMeteo_DescribesWmoWeatherCodes(int code, string expected) =>
        Assert.Equal(expected, OpenMeteoClient.DescribeWeatherCode(code));

    [Fact]
    public async Task HackerNews_MapsStories_WithRankBasedRelevance()
    {
        const string json = """
            {
              "hits": [
                { "objectID": "1", "title": "First", "url": "https://a.example", "author": "alice", "points": 100, "num_comments": 5, "created_at": "2026-11-20T10:00:00Z" },
                { "objectID": "2", "title": null, "url": null, "author": "x", "points": 1, "num_comments": 0, "created_at": "2026-11-20T10:00:00Z" },
                { "objectID": "3", "title": "Second", "url": null, "author": "bob", "points": 3, "num_comments": 1, "created_at": "2026-11-19T10:00:00Z" }
              ]
            }
            """;
        var handler = StubHttpMessageHandler.Json(json);
        var client = new HackerNewsClient(Http(handler, "https://hn.algolia.com/"), Options(HackerNewsClient.ApiName, new ExternalApiOptions { MaxItems = 3 }));

        var items = await client.FetchAsync(Request, CancellationToken.None);

        Assert.Equal(["First", "Second"], items.Select(i => i.Title)); // hits without a title are skipped
        Assert.Equal(1, items[0].Relevance);
        Assert.Equal(0.5, items[1].Relevance);
        Assert.Equal("https://news.ycombinator.com/item?id=3", items[1].Url); // falls back to the discussion
        Assert.Equal(ItemCategory.News, items[0].Category);
        Assert.Contains("query=dotnet", handler.Requests[0].ToString());
    }

    [Fact]
    public async Task GitHub_MapsRepositories_WithStarBasedRelevance_AndSendsTheToken()
    {
        const string json = """
            {
              "items": [
                { "full_name": "org/big", "description": "Big", "html_url": "https://github.com/org/big", "stargazers_count": 1000, "forks_count": 10, "language": "C#", "updated_at": "2026-11-01T00:00:00Z", "pushed_at": "2026-11-20T00:00:00Z" },
                { "full_name": "org/small", "description": null, "html_url": "https://github.com/org/small", "stargazers_count": 250, "forks_count": 1, "language": null, "updated_at": "2026-10-01T00:00:00Z", "pushed_at": null }
              ]
            }
            """;
        string? authorization = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            authorization = req.Headers.Authorization?.ToString();
            return StubHttpMessageHandler.JsonResponse(json);
        });
        var client = new GitHubClient(Http(handler, "https://api.github.com/"), Options(GitHubClient.ApiName, new ExternalApiOptions { ApiKey = "ghp_token" }));

        var items = await client.FetchAsync(Request, CancellationToken.None);

        Assert.Equal(["org/big", "org/small"], items.Select(i => i.Title));
        Assert.Equal(1, items[0].Relevance);
        Assert.Equal(0.25, items[1].Relevance);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero), items[0].Timestamp);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), items[1].Timestamp); // falls back to updated_at
        Assert.Equal("Bearer ghp_token", authorization);
    }

    [Fact]
    public async Task Clients_Throw_OnServerErrors_SoTheFetcherCanApplyTheFallback()
    {
        var client = new HackerNewsClient(
            Http(StubHttpMessageHandler.Json("{}", HttpStatusCode.ServiceUnavailable), "https://hn.algolia.com/"),
            Options(HackerNewsClient.ApiName, new ExternalApiOptions()));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchAsync(Request, CancellationToken.None));
    }

    [Fact]
    public void CacheKeys_OnlyDependOnTheParametersTheClientUses()
    {
        IExternalApiClient weather = new OpenMeteoClient(new HttpClient(), Options("x", new ExternalApiOptions()), TimeProvider.System);
        IExternalApiClient news = new HackerNewsClient(new HttpClient(), Options("x", new ExternalApiOptions()));

        Assert.Equal(weather.GetCacheKey(new ExternalApiRequest("Athens", "a")), weather.GetCacheKey(new ExternalApiRequest("Athens", "b")));
        Assert.Equal(news.GetCacheKey(new ExternalApiRequest("Athens", "a")), news.GetCacheKey(new ExternalApiRequest("London", "a")));
    }
}
