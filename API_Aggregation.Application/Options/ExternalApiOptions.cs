namespace API_Aggregation.Application.Options;

/// <summary>
/// Per-API settings. Bound as named options (name = API name) from <c>ExternalApis:{ApiName}</c>.
/// </summary>
public sealed class ExternalApiOptions
{
    public const string SectionName = "ExternalApis";

    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    /// <summary>How long a successful response is served from cache without calling the API again.</summary>
    public int CacheDurationSeconds { get; set; } = 300;

    /// <summary>How long the last successful response is kept as a fallback for when the API fails.</summary>
    public int StaleCacheDurationMinutes { get; set; } = 60;

    /// <summary>Maximum number of items requested from the API.</summary>
    public int MaxItems { get; set; } = 20;

    /// <summary>Timeout of a single HTTP attempt.</summary>
    public int AttemptTimeoutSeconds { get; set; } = 5;

    /// <summary>Timeout of the whole operation including retries.</summary>
    public int TotalTimeoutSeconds { get; set; } = 15;

    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>Free-form, API specific settings (e.g. secondary URLs).</summary>
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public TimeSpan CacheDuration => TimeSpan.FromSeconds(CacheDurationSeconds);

    public TimeSpan StaleCacheDuration => TimeSpan.FromMinutes(StaleCacheDurationMinutes);
}
