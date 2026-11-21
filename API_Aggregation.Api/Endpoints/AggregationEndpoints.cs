using System.ComponentModel;
using API_Aggregation.Application.CQRS.Queries.GetAggregatedData;
using API_Aggregation.Application.Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace API_Aggregation.Api.Endpoints;

public static class AggregationEndpoints
{
    public static RouteGroupBuilder MapAggregationEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/aggregation", GetAggregatedData)
            .WithName("GetAggregatedData")
            .WithTags("Aggregation")
            .WithSummary("Retrieve aggregated data from all external APIs")
            .WithDescription(
                "Calls the external APIs (weather, news, repositories) in parallel and returns a unified, " +
                "filtered, sorted and paged list of items, together with the status of every API " +
                "(Live, Cached, Stale fallback, Unavailable or Disabled).")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        return group;
    }

    private static async Task<Ok<AggregationResponseDto>> GetAggregatedData(
        ISender sender,
        CancellationToken cancellationToken,
        [Description("Location used by the weather APIs, e.g. 'Athens'. Required.")] string? city = null,
        [Description("Search term for the news / repository APIs. Defaults to the city.")] string? topic = null,
        [Description("Only call APIs of this category: Weather, News or Repository.")] string? category = null,
        [Description("Comma separated API names, e.g. 'OpenMeteo,HackerNews'.")] string? sources = null,
        [Description("Only items with a timestamp >= this date (ISO 8601).")] DateTimeOffset? from = null,
        [Description("Only items with a timestamp <= this date (ISO 8601).")] DateTimeOffset? to = null,
        [Description("Only items whose title or description contains this text.")] string? search = null,
        [Description("date (default), relevance, title or source.")] string? sortBy = null,
        [Description("desc (default) or asc.")] string? sortOrder = null,
        [Description("1-based page number (default 1).")] int? page = null,
        [Description("Items per page, 1-100 (default 20).")] int? pageSize = null)
    {
        var query = new GetAggregatedDataQuery(city, topic, category, sources, from, to, search, sortBy, sortOrder, page, pageSize);
        return TypedResults.Ok(await sender.Send(query, cancellationToken));
    }
}
