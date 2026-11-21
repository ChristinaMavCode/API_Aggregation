using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Dtos;
using FluentValidation;
using MediatR;

namespace API_Aggregation.Application.CQRS.Commands.GenerateToken;

/// <summary>Issues a JWT access token for valid credentials. Returns <c>null</c> for invalid credentials.</summary>
public sealed record GenerateTokenCommand(string? Username, string? Password) : IRequest<TokenResponseDto?>;

public sealed class GenerateTokenCommandValidator : AbstractValidator<GenerateTokenCommand>
{
    public GenerateTokenCommandValidator()
    {
        RuleFor(c => c.Username).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(200);
    }
}

public sealed class GenerateTokenCommandHandler(
    IUserCredentialValidator credentialValidator,
    ITokenService tokenService) : IRequestHandler<GenerateTokenCommand, TokenResponseDto?>
{
    public Task<TokenResponseDto?> Handle(GenerateTokenCommand command, CancellationToken cancellationToken)
    {
        var result = credentialValidator.IsValid(command.Username!, command.Password!)
            ? tokenService.CreateToken(command.Username!)
            : null;

        return Task.FromResult(result);
    }
}
