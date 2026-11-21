using API_Aggregation.Application.CQRS.Commands.GenerateToken;
using API_Aggregation.Application.Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace API_Aggregation.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/auth/token", _createToken)
            .WithName("CreateToken")
            .WithTags("Authentication")
            .WithSummary("Obtain a JWT access token")
            .WithDescription("Exchanges username / password for a Bearer token to be sent in the Authorization header.")
            .ProducesValidationProblem()
            .AllowAnonymous();

        return group;
    }

    private static async Task<Results<Ok<TokenResponseDto>, ProblemHttpResult>> _createToken(
        GenerateTokenCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var token = await sender.Send(command, cancellationToken);

        return token is null
            ? TypedResults.Problem(title: "Invalid username or password.", statusCode: StatusCodes.Status401Unauthorized)
            : TypedResults.Ok(token);
    }
}
