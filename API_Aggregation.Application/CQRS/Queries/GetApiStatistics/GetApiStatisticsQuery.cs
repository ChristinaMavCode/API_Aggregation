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

public sealed class GetApiStatisticsQueryHandler : IRequestHandler<GetApiStatisticsQuery, ApiStatisticsResponseDto>
{
    private readonly IApiStatisticsStore _statistics;
    private readonly IEnumerable<IExternalApiClient> _clients;
    private readonly AggregationMapper _mapper;
    private readonly IOptionsMonitor<StatisticsOptions> _optionsMonitor;
    private readonly TimeProvider _timeProvider;

    public GetApiStatisticsQueryHandler(IApiStatisticsStore statistics, IEnumerable<IExternalApiClient> clients,
        AggregationMapper mapper, IOptionsMonitor<StatisticsOptions> optionsMonitor, TimeProvider timeProvider)
    {
        _statistics = statistics;
        _clients = clients;
        _mapper = mapper;
        _optionsMonitor = optionsMonitor;
        _timeProvider = timeProvider;
    }

    public Task<ApiStatisticsResponseDto> Handle(GetApiStatisticsQuery request, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        var snapshots = _statistics.GetSnapshots().ToDictionary(s => s.ApiName, StringComparer.OrdinalIgnoreCase);

        // Registered APIs that haven't been called yet are reported with zero values,
        // so the consumer always sees the full list of integrated APIs.
        foreach (var client in _clients)
        {
            if (!snapshots.ContainsKey(client.Name))
            {
                snapshots.Add(client.Name, Empty(client.Name, options));
            }
        }

        var response = new ApiStatisticsResponseDto
        {
            GeneratedAt = _timeProvider.GetUtcNow(),
            FastThresholdMs = options.FastThresholdMs,
            SlowThresholdMs = options.SlowThresholdMs,
            Apis = _mapper.ToDtos(snapshots.Values.OrderBy(s => s.ApiName, StringComparer.OrdinalIgnoreCase))
        };

        return Task.FromResult(response);
    }

    private static ApiStatisticsSnapshot Empty(string apiName, StatisticsOptions options)
    {
        return new ApiStatisticsSnapshot(apiName, 0, 0, 0, Enum.GetValues<PerformanceBucket>()
            .Select(b =>
            {
                var (lower, upper) = PerformanceBucketClassifier.GetBounds(b, options);
                return new BucketStatistics(b, lower, upper, 0, 0);
            }).ToList());
    }
}
