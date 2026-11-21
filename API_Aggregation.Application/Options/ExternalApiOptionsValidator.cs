using Microsoft.Extensions.Options;

namespace API_Aggregation.Application.Options;

/// <summary>
/// Validates the per-API configuration (<c>ExternalApis:{Name}</c>) of the registered APIs. Combined with
/// <c>ValidateOnStart()</c>, an invalid value stops the application at startup with a clear message,
/// instead of surfacing later as an "Unavailable" API on every request.
/// (APIs without a configuration section are not registered, so they are never validated.)
/// </summary>
public sealed class ExternalApiOptionsValidator : IValidateOptions<ExternalApiOptions>
{
    public ValidateOptionsResult Validate(string? name, ExternalApiOptions options)
    {
        var section = $"{ExternalApiOptions.SectionName}:{name}";
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            errors.Add($"'{section}:BaseUrl' is required.");
        }
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add($"'{section}:BaseUrl' must be an absolute http(s) URL, but was '{options.BaseUrl}'.");
        }

        if (options.MaxItems < 1)
            errors.Add($"'{section}:MaxItems' must be at least 1.");

        if (options.CacheDurationSeconds < 1)
            errors.Add($"'{section}:CacheDurationSeconds' must be at least 1.");

        if (options.StaleCacheDurationMinutes < 1)
            errors.Add($"'{section}:StaleCacheDurationMinutes' must be at least 1.");

        if (options.AttemptTimeoutSeconds < 1)
            errors.Add($"'{section}:AttemptTimeoutSeconds' must be at least 1.");

        if (options.TotalTimeoutSeconds < options.AttemptTimeoutSeconds)
            errors.Add($"'{section}:TotalTimeoutSeconds' must be greater than or equal to AttemptTimeoutSeconds.");

        if (options.MaxRetryAttempts < 0)
            errors.Add($"'{section}:MaxRetryAttempts' cannot be negative.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
