using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Aggregation;
using API_Aggregation.Application.Dtos;
using API_Aggregation.Application.Mapping;
using API_Aggregation.Domain.Aggregation;
using MediatR;

namespace API_Aggregation.Application.CQRS.Queries.GetAggregatedData;

public sealed class GetAggregatedDataQueryHandler(
    IEnumerable<IExternalApiClient> clients,
    IExternalApiFetcher fetcher,
    AggregationMapper mapper,
    TimeProvider timeProvider) : IRequestHandler<GetAggregatedDataQuery, AggregationResponseDto>
{
    public async Task<AggregationResponseDto> Handle(GetAggregatedDataQuery query, CancellationToken cancellationToken)
    {
        var city = query.City!.Trim();
        var topic = string.IsNullOrWhiteSpace(query.Topic) ? city : query.Topic.Trim();
        var request = new ExternalApiRequest(city, topic);

        var selectedClients = SelectClients(query);

        // All selected APIs are queried in parallel. The fetcher never throws for API failures,
        // so one failing API cannot break the whole response.
        var results = await Task.WhenAll(selectedClients.Select(c => fetcher.FetchAsync(c, request, cancellationToken)));

        var criteria = new AggregationCriteria
        {
            From = query.From,
            To = query.To,
            Search = query.Search,
            SortBy = string.IsNullOrWhiteSpace(query.SortBy) ? AggregationSortBy.Date : Enum.Parse<AggregationSortBy>(query.SortBy, ignoreCase: true),
            Descending = !string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase),
            Page = query.Page ?? 1,
            PageSize = query.PageSize ?? GetAggregatedDataQuery.DefaultPageSize
        };

        var page = AggregatedItemsProcessor.Apply(results.SelectMany(r => r.Items), criteria);

        return new AggregationResponseDto
        {
            City = city,
            Topic = topic,
            GeneratedAt = timeProvider.GetUtcNow(),
            TotalItems = page.TotalItems,
            Page = criteria.Page,
            PageSize = criteria.PageSize,
            TotalPages = (int)Math.Ceiling(page.TotalItems / (double)criteria.PageSize),
            Sources = mapper.ToDtos(results),
            Items = mapper.ToDtos(page.Items)
        };
    }

    /// <summary>
    /// Category / source filters are applied before calling the APIs so that we never
    /// spend an external call on data that would be filtered out anyway.
    /// </summary>
    private IEnumerable<IExternalApiClient> SelectClients(GetAggregatedDataQuery query)
    {
        ItemCategory? category = string.IsNullOrWhiteSpace(query.Category)
            ? null
            : Enum.Parse<ItemCategory>(query.Category, ignoreCase: true);

        var sources = (query.Sources ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return clients.Where(c =>
            (category is null || c.Category == category) &&
            (sources.Count == 0 || sources.Contains(c.Name)));
    }
}
