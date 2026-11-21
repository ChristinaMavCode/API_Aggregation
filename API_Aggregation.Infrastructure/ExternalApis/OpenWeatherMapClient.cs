using System.Globalization;
using System.Text.Json.Serialization;
using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Options;
using API_Aggregation.Domain.Aggregation;
using Microsoft.Extensions.Options;

namespace API_Aggregation.Infrastructure.ExternalApis;

/// <summary>
/// OpenWeatherMap 5 day / 3 hour forecast (https://openweathermap.org/forecast5). Requires a free API key.
/// </summary>
public sealed class OpenWeatherMapClient(HttpClient httpClient, IOptionsMonitor<ExternalApiOptions> optionsMonitor, TimeProvider timeProvider)
    : ExternalApiClientBase(httpClient, optionsMonitor)
{
    public const string ApiName = "OpenWeatherMap";

    public override string Name => ApiName;

    public override ItemCategory Category => ItemCategory.Weather;

    public override bool RequiresApiKey => true;

    // Weather only depends on the city; the topic must not fragment the cache.
    public override string GetCacheKey(ExternalApiRequest request) => request.City;

    public override async Task<IReadOnlyList<AggregatedItem>> FetchAsync(ExternalApiRequest request, CancellationToken cancellationToken)
    {
        var options = Options;
        var uri = $"data/2.5/forecast?q={Uri.EscapeDataString(request.City)}&units=metric&cnt={options.MaxItems}&appid={Uri.EscapeDataString(options.ApiKey ?? string.Empty)}";

        var response = await GetJsonAsync<ForecastResponse>(uri, cancellationToken);
        if (response?.List is null)
            return [];

        var now = timeProvider.GetUtcNow();
        var cityName = response.City?.Name ?? request.City;
        var url = response.City?.Id is { } id ? $"https://openweathermap.org/city/{id}" : null;

        return response.List
            .Select(entry =>
            {
                var timestamp = DateTimeOffset.FromUnixTimeSeconds(entry.Dt);
                var condition = entry.Weather?.FirstOrDefault();
                var temperature = entry.Main?.Temp ?? 0;

                return new AggregatedItem
                {
                    Source = Name,
                    Category = Category,
                    Title = $"{cityName}: {temperature.ToString("0.#", CultureInfo.InvariantCulture)}°C, {condition?.Description ?? "n/a"}",
                    Description = $"Forecast for {timestamp:yyyy-MM-dd HH:mm} UTC. Feels like {entry.Main?.FeelsLike.ToString("0.#", CultureInfo.InvariantCulture)}°C, humidity {entry.Main?.Humidity}%, wind {entry.Wind?.Speed.ToString("0.#", CultureInfo.InvariantCulture)} m/s.",
                    Timestamp = timestamp,
                    Relevance = TimeProximityRelevance(timestamp, now),
                    Url = url,
                    Metadata = new Dictionary<string, string>
                    {
                        ["temperatureC"] = temperature.ToString(CultureInfo.InvariantCulture),
                        ["humidity"] = (entry.Main?.Humidity ?? 0).ToString(CultureInfo.InvariantCulture),
                        ["windSpeedMs"] = (entry.Wind?.Speed ?? 0).ToString(CultureInfo.InvariantCulture),
                        ["condition"] = condition?.Main ?? string.Empty,
                        ["country"] = response.City?.Country ?? string.Empty
                    }
                };
            })
            .ToList();
    }

    private sealed record ForecastResponse(List<ForecastEntry>? List, CityInfo? City);

    private sealed record ForecastEntry(long Dt, MainInfo? Main, List<WeatherInfo>? Weather, WindInfo? Wind);

    private sealed record MainInfo(
        double Temp,
        [property: JsonPropertyName("feels_like")] double FeelsLike,
        int Humidity);

    private sealed record WeatherInfo(string? Main, string? Description);

    private sealed record WindInfo(double Speed);

    private sealed record CityInfo(long? Id, string? Name, string? Country);
}
