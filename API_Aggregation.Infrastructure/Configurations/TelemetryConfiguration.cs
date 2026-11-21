using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace API_Aggregation.Infrastructure.Configurations;

public static class TelemetryConfiguration
{
    private const string DefaultOtlpEndpoint = "http://localhost:4317";

    /// <summary>
    /// Adds OpenTelemetry tracing (incoming requests + outgoing external API calls) exported over
    /// OTLP/gRPC to Jaeger (or any OTLP collector) at <c>OpenTelemetry:OtlpEndpoint</c>.
    /// Defaults to <c>http://localhost:4317</c> when the key is missing; set it to an empty value to disable exporting.
    /// If the collector is not running, spans are dropped silently and the API is not affected.
    /// </summary>
    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        string serviceName,
        IConfiguration configuration,
        params string[] additionalSources)
    {
        var endpoint = configuration["OpenTelemetry:OtlpEndpoint"] ?? DefaultOtlpEndpoint;

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
