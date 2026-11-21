using System.Text;
using API_Aggregation.Application.Options;
using API_Aggregation.Infrastructure.Authentication;
using API_Aggregation.UnitTests.TestHelpers;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace API_Aggregation.UnitTests.Authentication;

public class AuthenticationTests
{
    private static readonly JwtOptions Jwt = new()
    {
        Key = "unit-test-signing-key-that-is-long-enough-1234567890",
        Issuer = "test-issuer",
        Audience = "test-audience",
        ExpiryMinutes = 15
    };

    [Fact]
    public async Task JwtTokenService_CreatesAValidSignedToken()
    {
        var now = DateTimeOffset.UtcNow;
        var service = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(Jwt), new FakeTimeProvider(now));

        var token = service.CreateToken("demo");

        Assert.Equal("Bearer", token.TokenType);
        Assert.Equal(now.AddMinutes(15), token.ExpiresAt);

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(token.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = Jwt.Issuer,
            ValidAudience = Jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Jwt.Key))
        });

        Assert.True(validation.IsValid);
        Assert.Equal("demo", validation.Claims[JwtRegisteredClaimNames.Sub]);
    }

    [Fact]
    public async Task JwtTokenService_TokenIsRejected_WithADifferentKey()
    {
        var token = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(Jwt), TimeProvider.System).CreateToken("demo");

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(token.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = Jwt.Issuer,
            ValidAudience = Jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("another-key-another-key-another-key-123"))
        });

        Assert.False(validation.IsValid);
    }

    [Theory]
    [InlineData("demo", "demo-password", true)]
    [InlineData("DEMO", "demo-password", true)]   // usernames are case-insensitive
    [InlineData("demo", "DEMO-PASSWORD", false)]  // passwords are not
    [InlineData("demo", "wrong", false)]
    [InlineData("unknown", "demo-password", false)]
    public void CredentialValidator_ValidatesAgainstConfiguredUsers(string username, string password, bool expected)
    {
        var options = new AuthUsersOptions { Users = [new AuthUser { Username = "demo", Password = "demo-password" }] };
        var validator = new ConfigurationUserCredentialValidator(new TestOptionsMonitor<AuthUsersOptions>(options));

        Assert.Equal(expected, validator.IsValid(username, password));
    }
}
