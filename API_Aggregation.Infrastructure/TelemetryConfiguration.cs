using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace API_Aggregation.Infrastructure;

public static class TelemetryConfiguration
{
    /// <summary>
    /// Adds OpenTelemetry tracing (incoming requests + outgoing external API calls).
    /// The OTLP exporter is only enabled when <c>OpenTelemetry:OtlpEndpoint</c> is configured,
    /// so the service runs without a collector out of the box.
    /// </summary>
    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        string serviceName,
        IConfiguration configuration,
        params string[] additionalSources)
    {
        var endpoint = configuration["OpenTelemetry:OtlpEndpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation()
                 .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    t.AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(endpoint);
                        o.Protocol = OtlpExportProtocol.Grpc;
                    });
                }

                foreach (var source in additionalSources)
                    t.AddSource(source);
            });

        return services;
    }
}
