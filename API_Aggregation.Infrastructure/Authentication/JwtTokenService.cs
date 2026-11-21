using System.Security.Claims;
using System.Text;
using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Dtos;
using API_Aggregation.Application.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace API_Aggregation.Infrastructure.Authentication;

/// <summary>Creates HMAC-SHA256 signed JWT access tokens.</summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly IOptions<JwtOptions> _options;
    private readonly TimeProvider _timeProvider;
    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
    }
    private static readonly JsonWebTokenHandler TokenHandler = new JsonWebTokenHandler();

    public TokenResponseDto CreateToken(string username)
    {
        var jwt = _options.Value;
        var now = _timeProvider.GetUtcNow();
        var expires = now.AddMinutes(jwt.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.UniqueName, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                SecurityAlgorithms.HmacSha256)
        };

        return new TokenResponseDto(TokenHandler.CreateToken(descriptor), expires);
    }
}
