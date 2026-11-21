using API_Aggregation.Application.Dtos;
using MediatR;

namespace API_Aggregation.Application.CQRS.Queries.GetAggregatedData;

/// <summary>
/// Retrieves aggregated data from all (matching) external APIs.
/// </summary>
/// <param name="City">Location for the weather APIs (required).</param>
/// <param name="Topic">Search term for the news / repository APIs. Defaults to <paramref name="City"/>.</param>
/// <param name="Category">Only call APIs of this category: Weather, News or Repository.</param>
/// <param name="Sources">Comma separated list of API names to call (e.g. "OpenMeteo,HackerNews").</param>
/// <param name="From">Only items with a timestamp at or after this date.</param>
/// <param name="To">Only items with a timestamp at or before this date.</param>
/// <param name="Search">Only items whose title or description contains this text.</param>
/// <param name="SortBy">date (default), relevance, title or source.</param>
/// <param name="SortOrder">desc (default) or asc.</param>
/// <param name="Page">1-based page number (default 1).</param>
/// <param name="PageSize">Items per page, 1-100 (default 20).</param>
public sealed record GetAggregatedDataQuery(
    string? City,
    string? Topic = null,
    string? Category = null,
    string? Sources = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Search = null,
    string? SortBy = null,
    string? SortOrder = null,
    int? Page = null,
    int? PageSize = null) : IRequest<AggregationResponseDto>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
