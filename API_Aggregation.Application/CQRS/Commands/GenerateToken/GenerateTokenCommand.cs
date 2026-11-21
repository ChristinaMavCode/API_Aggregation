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

public sealed class GenerateTokenCommandHandler : IRequestHandler<GenerateTokenCommand, TokenResponseDto?>
{
    private readonly IUserCredentialValidator _credentialValidator;
    private readonly ITokenService _tokenService;

    public GenerateTokenCommandHandler(IUserCredentialValidator credentialValidator, ITokenService tokenService)
    {
        _credentialValidator = credentialValidator;
        _tokenService = tokenService;
    }

    public Task<TokenResponseDto?> Handle(GenerateTokenCommand command, CancellationToken cancellationToken)
    {
        var result = _credentialValidator.IsValid(command.Username!, command.Password!)
            ? _tokenService.CreateToken(command.Username!)
            : null;

        return Task.FromResult(result);
    }
}
