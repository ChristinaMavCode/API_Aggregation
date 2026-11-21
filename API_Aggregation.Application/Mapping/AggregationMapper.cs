using API_Aggregation.Application.Dtos;
using API_Aggregation.Domain.Aggregation;
using API_Aggregation.Domain.Statistics;
using Riok.Mapperly.Abstractions;

namespace API_Aggregation.Application.Mapping;

/// <summary>
/// Compile-time generated (Mapperly) mappings from domain models to response DTOs.
/// Enums are mapped to their names so that the JSON output is human readable.
/// </summary>
[Mapper]
public partial class AggregationMapper
{
    public partial AggregatedItemDto ToDto(AggregatedItem item);

    public partial IReadOnlyList<AggregatedItemDto> ToDtos(IEnumerable<AggregatedItem> items);

    [MapProperty([nameof(SourceResult.Items), nameof(IReadOnlyCollection<AggregatedItem>.Count)], nameof(SourceStatusDto.ItemCount))]
    public partial SourceStatusDto ToDto(SourceResult result);

    public partial IReadOnlyList<SourceStatusDto> ToDtos(IEnumerable<SourceResult> results);

    public partial ApiStatisticsDto ToDto(ApiStatisticsSnapshot snapshot);

    public partial IReadOnlyList<ApiStatisticsDto> ToDtos(IEnumerable<ApiStatisticsSnapshot> snapshots);

    private partial BucketStatisticsDto _toDto(BucketStatistics bucket);
}
