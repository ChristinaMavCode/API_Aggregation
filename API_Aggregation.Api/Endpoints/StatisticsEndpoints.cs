using API_Aggregation.Application.CQRS.Queries.GetApiStatistics;
using API_Aggregation.Application.Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace API_Aggregation.Api.Endpoints;

public static class StatisticsEndpoints
{
    public static RouteGroupBuilder MapStatisticsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/statistics", GetStatistics)
            .WithName("GetApiStatistics")
            .WithTags("Statistics")
            .WithSummary("Retrieve request statistics per external API")
            .WithDescription(
                "For each external API returns the total number of requests, failed requests and the average " +
                "response time, grouped in performance buckets (Fast / Average / Slow). Cache hits are not counted, " +
                "only real calls to the external APIs.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        return group;
    }

    private static async Task<Ok<ApiStatisticsResponseDto>> GetStatistics(ISender sender, CancellationToken cancellationToken) =>
        TypedResults.Ok(await sender.Send(new GetApiStatisticsQuery(), cancellationToken));
}
