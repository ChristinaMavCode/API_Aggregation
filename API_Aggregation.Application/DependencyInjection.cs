using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Aggregation;
using API_Aggregation.Application.CQRS.Behaviors;
using API_Aggregation.Application.Mapping;
using API_Aggregation.Application.Statistics;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace API_Aggregation.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationConfiguration(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddMemoryCache();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AggregationMapper>();
        services.AddSingleton<IExternalApiFetcher, ExternalApiFetcher>();
        services.AddSingleton<PerformanceAnomalyDetector>();

        return services;
    }
}
