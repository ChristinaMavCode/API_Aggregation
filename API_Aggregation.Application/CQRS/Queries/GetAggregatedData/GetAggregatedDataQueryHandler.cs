using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Aggregation;
using API_Aggregation.Application.Dtos;
using API_Aggregation.Application.Mapping;
using API_Aggregation.Domain.Aggregation;
using MediatR;

namespace API_Aggregation.Application.CQRS.Queries.GetAggregatedData;

public sealed class GetAggregatedDataQueryHandler : IRequestHandler<GetAggregatedDataQuery, AggregationResponseDto>
{
    private readonly IEnumerable<IExternalApiClient> _clients;
    private readonly IExternalApiFetcher _fetcher;
    private readonly AggregationMapper _mapper;
    private readonly TimeProvider _timeProvider;

    public GetAggregatedDataQueryHandler(
        IEnumerable<IExternalApiClient> clients,
        IExternalApiFetcher fetcher,
        AggregationMapper mapper,
        TimeProvider timeProvider)
    {
        _clients = clients;
        _fetcher = fetcher;
        _mapper = mapper;
        _timeProvider = timeProvider;
    }

    public async Task<AggregationResponseDto> Handle(GetAggregatedDataQuery query, CancellationToken cancellationToken)
    {
        var city = query.City!.Trim();
        var topic = string.IsNullOrWhiteSpace(query.Topic) ? city : query.Topic.Trim();
        var request = new ExternalApiRequest(city, topic);

        var selectedClients = SelectClients(query);

        // All selected APIs are queried in parallel. The fetcher never throws for API failures,
        // so one failing API cannot break the whole response.
        var results = await Task.WhenAll(selectedClients.Select(c => _fetcher.FetchAsync(c, request, cancellationToken)));

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
            GeneratedAt = _timeProvider.GetUtcNow(),
            TotalItems = page.TotalItems,
            Page = criteria.Page,
            PageSize = criteria.PageSize,
            TotalPages = (int)Math.Ceiling(page.TotalItems / (double)criteria.PageSize),
            Sources = _mapper.ToDtos(results),
            Items = _mapper.ToDtos(page.Items)
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

        return _clients.Where(c =>
            (category is null || c.Category == category) &&
            (sources.Count == 0 || sources.Contains(c.Name)));
    }
}
