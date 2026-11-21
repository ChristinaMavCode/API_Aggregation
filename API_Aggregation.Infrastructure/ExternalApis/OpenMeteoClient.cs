using System.Globalization;
using System.Text.Json.Serialization;
using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Options;
using API_Aggregation.Domain.Aggregation;
using Microsoft.Extensions.Options;

namespace API_Aggregation.Infrastructure.ExternalApis;

/// <summary>
/// Open-Meteo daily forecast (https://open-meteo.com). No API key required.
/// The city is first resolved to coordinates with the Open-Meteo geocoding API.
/// </summary>
public sealed class OpenMeteoClient : ExternalApiClientBase
{
    public const string ApiName = "OpenMeteo";
    private const string DefaultGeocodingUrl = "https://geocoding-api.open-meteo.com/v1/search";

    private readonly TimeProvider _timeProvider;

    public OpenMeteoClient(HttpClient httpClient, IOptionsMonitor<ExternalApiOptions> optionsMonitor, TimeProvider timeProvider)
        : base(httpClient, optionsMonitor)
    {
        _timeProvider = timeProvider;
    }

    public override string Name => ApiName;

    public override ItemCategory Category => ItemCategory.Weather;

    public override string GetCacheKey(ExternalApiRequest request) => request.City;

    public override async Task<IReadOnlyList<AggregatedItem>> FetchAsync(ExternalApiRequest request, CancellationToken cancellationToken)
    {
        var options = Options;
        var geocodingUrl = options.Settings.GetValueOrDefault("GeocodingUrl", DefaultGeocodingUrl);

        var geocoding = await GetJsonAsync<GeocodingResponse>(
            $"{geocodingUrl}?name={Uri.EscapeDataString(request.City)}&count=1&language=en&format=json", cancellationToken);

        var location = geocoding?.Results?.FirstOrDefault();
        if (location is null)
            return [];

        var forecastDays = Math.Clamp(options.MaxItems, 1, 16);
        var lat = location.Latitude.ToString(CultureInfo.InvariantCulture);
        var lon = location.Longitude.ToString(CultureInfo.InvariantCulture);

        var forecast = await GetJsonAsync<ForecastResponse>(
            $"v1/forecast?latitude={lat}&longitude={lon}&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_sum,wind_speed_10m_max&timezone=auto&forecast_days={forecastDays}",
            cancellationToken);

        var daily = forecast?.Daily;
        if (daily?.Time is null)
            return [];

        var now = _timeProvider.GetUtcNow();
        var offset = TimeSpan.FromSeconds(forecast!.UtcOffsetSeconds);
        var url = $"https://open-meteo.com/en/docs#latitude={lat}&longitude={lon}";
        var items = new List<AggregatedItem>(daily.Time.Count);

        for (var i = 0; i < daily.Time.Count; i++)
        {
            var date = DateTime.ParseExact(daily.Time[i], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var timestamp = new DateTimeOffset(date, offset);
            var code = At(daily.WeatherCode, i);
            var max = At(daily.TemperatureMax, i);
            var min = At(daily.TemperatureMin, i);
            var precipitation = At(daily.PrecipitationSum, i);
            var wind = At(daily.WindSpeedMax, i);
            var condition = DescribeWeatherCode((int)code);

            items.Add(new AggregatedItem
            {
                Source = Name,
                Category = Category,
                Title = $"{location.Name}: {min.ToString("0.#", CultureInfo.InvariantCulture)}–{max.ToString("0.#", CultureInfo.InvariantCulture)}°C, {condition}",
                Description = $"Daily forecast for {daily.Time[i]}. Precipitation {precipitation.ToString("0.#", CultureInfo.InvariantCulture)} mm, max wind {wind.ToString("0.#", CultureInfo.InvariantCulture)} km/h.",
                Timestamp = timestamp,
                Relevance = TimeProximityRelevance(timestamp, now),
                Url = url,
                Metadata = new Dictionary<string, string>
                {
                    ["temperatureMinC"] = min.ToString(CultureInfo.InvariantCulture),
                    ["temperatureMaxC"] = max.ToString(CultureInfo.InvariantCulture),
                    ["precipitationMm"] = precipitation.ToString(CultureInfo.InvariantCulture),
                    ["windSpeedMaxKmh"] = wind.ToString(CultureInfo.InvariantCulture),
                    ["condition"] = condition,
                    ["country"] = location.Country ?? string.Empty
                }
            });
        }

        return items;
    }

    private static double At(List<double?>? values, int index) =>
        values is not null && index < values.Count ? values[index] ?? 0 : 0;

    /// <summary>WMO weather interpretation codes (https://open-meteo.com/en/docs).</summary>
    internal static string DescribeWeatherCode(int code) => code switch
    {
        0 => "clear sky",
        1 or 2 => "partly cloudy",
        3 => "overcast",
        45 or 48 => "fog",
        >= 51 and <= 57 => "drizzle",
        >= 61 and <= 67 => "rain",
        >= 71 and <= 77 => "snow",
        >= 80 and <= 82 => "rain showers",
        85 or 86 => "snow showers",
        >= 95 => "thunderstorm",
        _ => "unknown"
    };

    private sealed record GeocodingResponse(List<GeocodingResult>? Results);

    private sealed record GeocodingResult(string Name, double Latitude, double Longitude, string? Country);

    private sealed record ForecastResponse(
        [property: JsonPropertyName("utc_offset_seconds")] int UtcOffsetSeconds,
        DailyForecast? Daily);

    private sealed record DailyForecast(
        List<string>? Time,
        [property: JsonPropertyName("weather_code")] List<double?>? WeatherCode,
        [property: JsonPropertyName("temperature_2m_max")] List<double?>? TemperatureMax,
        [property: JsonPropertyName("temperature_2m_min")] List<double?>? TemperatureMin,
        [property: JsonPropertyName("precipitation_sum")] List<double?>? PrecipitationSum,
        [property: JsonPropertyName("wind_speed_10m_max")] List<double?>? WindSpeedMax);
}
