using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Options;
using API_Aggregation.Infrastructure.ExternalApis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace API_Aggregation.Infrastructure.Configurations;

public static class ExternalApisConfiguration
{
    private const string UserAgent = "API-Aggregation-Service/1.0";

    public static IServiceCollection AddExternalApis(this IServiceCollection services, IConfiguration configuration)
    {
        // Adding a new API = one IExternalApiClient implementation + one line here + one config section.
        services.AddExternalApiClient<OpenWeatherMapClient>(OpenWeatherMapClient.ApiName, configuration);
        services.AddExternalApiClient<OpenMeteoClient>(OpenMeteoClient.ApiName, configuration);
        services.AddExternalApiClient<HackerNewsClient>(HackerNewsClient.ApiName, configuration);
        services.AddExternalApiClient<GitHubClient>(GitHubClient.ApiName, configuration);

        return services;
    }

    /// <summary>
    /// Registers a typed <see cref="HttpClient"/> for the API together with its named options and a
    /// resilience pipeline (Polly through Microsoft.Extensions.Http.Resilience):
    /// rate limiter → total timeout → retry (exponential back-off + jitter, transient errors only:
    /// 5xx, 408, 429, network failures) → circuit breaker → per-attempt timeout.
    /// </summary>
    public static IServiceCollection AddExternalApiClient<TClient>(this IServiceCollection services, string name, IConfiguration configuration)
        where TClient : class, IExternalApiClient
    {
        var section = configuration.GetSection($"{ExternalApiOptions.SectionName}:{name}");
        services.AddOptions<ExternalApiOptions>(name).Bind(section);

        var options = section.Get<ExternalApiOptions>() ?? new ExternalApiOptions();

        services
            .AddHttpClient<TClient>(client =>
            {
                if (!string.IsNullOrWhiteSpace(options.BaseUrl))
                    client.BaseAddress = new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/");

                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                // The resilience pipeline owns the timeouts.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddStandardResilienceHandler(resilience =>
            {
                var attemptTimeout = TimeSpan.FromSeconds(Math.Max(1, options.AttemptTimeoutSeconds));
                var totalTimeout = TimeSpan.FromSeconds(Math.Max(options.TotalTimeoutSeconds, options.AttemptTimeoutSeconds));

                resilience.AttemptTimeout.Timeout = attemptTimeout;
                resilience.TotalRequestTimeout.Timeout = totalTimeout;
                resilience.Retry.MaxRetryAttempts = Math.Max(0, options.MaxRetryAttempts);
                resilience.Retry.UseJitter = true;
                resilience.Retry.Delay = TimeSpan.FromMilliseconds(300);
                // The circuit breaker sampling window must be at least twice the attempt timeout.
                resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(Math.Max(30, attemptTimeout.TotalSeconds * 2));
                resilience.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            });

        // Expose the typed client through the common abstraction so it's picked up by the aggregation.
        services.AddTransient<IExternalApiClient>(sp => sp.GetRequiredService<TClient>());
        return services;
    }
}
