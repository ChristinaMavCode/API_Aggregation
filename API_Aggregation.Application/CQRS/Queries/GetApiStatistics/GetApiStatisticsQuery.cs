using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Dtos;
using API_Aggregation.Application.Mapping;
using API_Aggregation.Application.Options;
using API_Aggregation.Application.Statistics;
using API_Aggregation.Domain.Statistics;
using MediatR;
using Microsoft.Extensions.Options;

namespace API_Aggregation.Application.CQRS.Queries.GetApiStatistics;

/// <summary>Retrieves request statistics (count, average response time and performance buckets) per external API.</summary>
public sealed record GetApiStatisticsQuery : IRequest<ApiStatisticsResponseDto>;

public sealed class GetApiStatisticsQueryHandler(
    IApiStatisticsStore statistics,
    IEnumerable<IExternalApiClient> clients,
    AggregationMapper mapper,
    IOptionsMonitor<StatisticsOptions> optionsMonitor,
    TimeProvider timeProvider) : IRequestHandler<GetApiStatisticsQuery, ApiStatisticsResponseDto>
{
    public Task<ApiStatisticsResponseDto> Handle(GetApiStatisticsQuery request, CancellationToken cancellationToken)
    {
        var options = optionsMonitor.CurrentValue;
        var snapshots = statistics.GetSnapshots().ToDictionary(s => s.ApiName, StringComparer.OrdinalIgnoreCase);

        // Registered APIs that haven't been called yet are reported with zero values,
        // so the consumer always sees the full list of integrated APIs.
        foreach (var client in clients)
            snapshots.TryAdd(client.Name, Empty(client.Name, options));

        var response = new ApiStatisticsResponseDto
        {
            GeneratedAt = timeProvider.GetUtcNow(),
            FastThresholdMs = options.FastThresholdMs,
            SlowThresholdMs = options.SlowThresholdMs,
            Apis = mapper.ToDtos(snapshots.Values.OrderBy(s => s.ApiName, StringComparer.OrdinalIgnoreCase))
        };

        return Task.FromResult(response);
    }

    private static ApiStatisticsSnapshot Empty(string apiName, StatisticsOptions options) =>
        new(apiName, 0, 0, 0, Enum.GetValues<PerformanceBucket>()
            .Select(b =>
            {
                var (lower, upper) = PerformanceBucketClassifier.GetBounds(b, options);
                return new BucketStatistics(b, lower, upper, 0, 0);
            })
            .ToList());
}
