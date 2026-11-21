using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using API_Aggregation.Application;
using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Middleware;
using API_Aggregation.Application.Options;
using API_Aggregation.Infrastructure.BackgroundServices;
using API_Aggregation.Infrastructure.Statistics;
using System.Security.Claims;
using Serilog;

namespace API_Aggregation.Infrastructure.Configurations;

public static class DefaultConfiguration
{
    public static IServiceCollection AddDefaultConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        services.AddApplicationConfiguration();

        services.AddExternalApis(configuration);

        services.AddOptions<StatisticsOptions>().Bind(configuration.GetSection(StatisticsOptions.SectionName));
        services.AddOptions<AnomalyDetectionOptions>().Bind(configuration.GetSection(AnomalyDetectionOptions.SectionName));

        services.AddSingleton<IApiStatisticsStore, InMemoryApiStatisticsStore>();
        services.AddHostedService<PerformanceAnomalyBackgroundService>();

        return services;
    }

    public static void UseCommonMiddleware(this WebApplication app)
    {
        app.UseMiddleware<ValidationExceptionMiddleware>();
        app.UseMiddleware<LogContextMiddleware>();
        app.UseSerilogRequestLogging(opts =>
        {
            opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("UserId", httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous");
                diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault() ?? "unknown");
            };
        });
    }
}
