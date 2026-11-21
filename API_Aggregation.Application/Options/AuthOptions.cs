namespace API_Aggregation.Application.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public double ExpiryMinutes { get; set; } = 30;
}

/// <summary>
/// Demo users allowed to obtain a token. In a real system this would be an identity provider / user store.
/// </summary>
public sealed class AuthUsersOptions
{
    public const string SectionName = "Auth";

    public List<AuthUser> Users { get; set; } = [];
}

public sealed class AuthUser
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
