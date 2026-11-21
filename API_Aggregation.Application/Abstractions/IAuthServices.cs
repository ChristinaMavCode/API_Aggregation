using API_Aggregation.Application.Dtos;

namespace API_Aggregation.Application.Abstractions;

/// <summary>Issues JWT access tokens.</summary>
public interface ITokenService
{
    TokenResponseDto CreateToken(string username);
}

/// <summary>Validates user credentials.</summary>
public interface IUserCredentialValidator
{
    bool IsValid(string username, string password);
}
