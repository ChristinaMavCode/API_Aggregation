using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace API_Aggregation.Infrastructure;

public static class TelemetryConfiguration
{
    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        string serviceName,
        IConfiguration configuration,
        params string[] additionalSources)
    {
        var endpoint = configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation()
                 .AddHttpClientInstrumentation()
                 .AddGrpcClientInstrumentation()
                 .AddSqlClientInstrumentation()
                 .AddOtlpExporter(o => 
                    {
                        o.Endpoint = new Uri(endpoint);
                        //This can be omitted as it is the default
                        o.Protocol = OtlpExportProtocol.Grpc;
                    }
                 );

                foreach (var source in additionalSources)
                    t.AddSource(source);
            });

        return services;
    }
}
