using System.Security.Cryptography;
using System.Text;
using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Options;
using Microsoft.Extensions.Options;

namespace API_Aggregation.Infrastructure.Authentication;

/// <summary>
/// Validates credentials against the demo users configured under <c>Auth:Users</c>.
/// Passwords are compared in constant time to avoid timing attacks.
/// NOTE: demo only - a real deployment would delegate to an identity provider.
/// </summary>
public sealed class ConfigurationUserCredentialValidator(IOptionsMonitor<AuthUsersOptions> optionsMonitor) : IUserCredentialValidator
{
    public bool IsValid(string username, string password)
    {
        var user = optionsMonitor.CurrentValue.Users
            .FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

        if (user is null || string.IsNullOrEmpty(user.Password))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(user.Password),
            Encoding.UTF8.GetBytes(password));
    }
}
