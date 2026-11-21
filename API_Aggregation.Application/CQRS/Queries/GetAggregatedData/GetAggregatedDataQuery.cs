using System.ComponentModel;
using API_Aggregation.Application.Dtos;
using MediatR;

namespace API_Aggregation.Application.CQRS.Queries.GetAggregatedData;

/// <summary>
/// Retrieves aggregated data from all (matching) external APIs.
/// Bound directly from the query string of <c>GET /api/v1/aggregation</c> (<c>[AsParameters]</c>);
/// the descriptions appear in the OpenAPI document.
/// </summary>
public sealed record GetAggregatedDataQuery(
    [Description("Location used by the weather APIs, e.g. 'Athens'. Required.")]
    string? City,
    [Description("Search term for the news / repository APIs. Defaults to the city.")]
    string? Topic = null,
    [Description("Only call APIs of this category: Weather, News or Repository.")]
    string? Category = null,
    [Description("Comma separated API names, e.g. 'OpenMeteo,HackerNews'.")]
    string? Sources = null,
    [Description("Only items with a timestamp >= this date (ISO 8601).")]
    DateTimeOffset? From = null,
    [Description("Only items with a timestamp <= this date (ISO 8601).")]
    DateTimeOffset? To = null,
    [Description("Only items whose title or description contains this text.")]
    string? Search = null,
    [Description("date (default), relevance, title or source.")]
    string? SortBy = null,
    [Description("desc (default) or asc.")]
    string? SortOrder = null,
    [Description("1-based page number (default 1).")]
    int? Page = null,
    [Description("Items per page, 1-100 (default 20).")]
    int? PageSize = null) : IRequest<AggregationResponseDto>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
