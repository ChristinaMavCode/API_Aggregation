using StartUp.Application.UserAuth;

namespace StartUp.UnitTests;

public class InMemoryRefreshTokenStoreTests
{
    private readonly InMemoryRefreshTokenStore _store;

    public InMemoryRefreshTokenStoreTests()
    {
        _store = new InMemoryRefreshTokenStore();
    }

    #region Store Tests

    [Fact]
    public async Task StoreAsync_StoresToken_Successfully()
    {
        // Arrange
        var userId = "1";
        var refreshToken = Guid.NewGuid().ToString();
        var expiration = DateTime.UtcNow.AddDays(7);

        // Act
        await _store.StoreAsync(userId, refreshToken, expiration);

        // Assert
        var result = await _store.ValidateAsync(refreshToken);
        Assert.Equal(userId, result);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsUserId_When_TokenIsValid()
    {
        // Arrange
        var userId = "42";
        var refreshToken = Guid.NewGuid().ToString();
        var expiration = DateTime.UtcNow.AddDays(7);

        await _store.StoreAsync(userId, refreshToken, expiration);

        // Act
        var result = await _store.ValidateAsync(refreshToken);

        // Assert
        Assert.Equal(userId, result);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsNull_When_TokenDoesNotExist()
    {
        // Act
        var result = await _store.ValidateAsync("nonexistent-token");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsNull_When_TokenIsExpired()
    {
        // Arrange
        var userId = "1";
        var refreshToken = Guid.NewGuid().ToString();
        var expiration = DateTime.UtcNow.AddSeconds(-1); // Already expired

        await _store.StoreAsync(userId, refreshToken, expiration);

        // Act
        var result = await _store.ValidateAsync(refreshToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsNull_When_TokenIsRevoked()
    {
        // Arrange
        var userId = "1";
        var refreshToken = Guid.NewGuid().ToString();
        var expiration = DateTime.UtcNow.AddDays(7);

        await _store.StoreAsync(userId, refreshToken, expiration);
        await _store.RevokeAsync(refreshToken);

        // Act
        var result = await _store.ValidateAsync(refreshToken);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Revoke Tests

    [Fact]
    public async Task RevokeAsync_InvalidatesToken()
    {
        // Arrange
        var userId = "1";
        var refreshToken = Guid.NewGuid().ToString();
        var expiration = DateTime.UtcNow.AddDays(7);

        await _store.StoreAsync(userId, refreshToken, expiration);

        // Act
        await _store.RevokeAsync(refreshToken);
        var result = await _store.ValidateAsync(refreshToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RevokeAsync_DoesNotThrow_For_NonexistentToken()
    {
        // Act & Assert
        await _store.RevokeAsync("nonexistent-token"); // Should not throw
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task StoreAsync_AllowsMultipleTokensForSameUser()
    {
        // Arrange
        var userId = "1";
        var token1 = Guid.NewGuid().ToString();
        var token2 = Guid.NewGuid().ToString();
        var expiration = DateTime.UtcNow.AddDays(7);

        // Act
        await _store.StoreAsync(userId, token1, expiration);
        await _store.StoreAsync(userId, token2, expiration);

        var result1 = await _store.ValidateAsync(token1);
        var result2 = await _store.ValidateAsync(token2);

        // Assert
        Assert.Equal(userId, result1);
        Assert.Equal(userId, result2);
    }

    [Fact]
    public async Task ValidateAsync_DoesNotAffectOtherTokens_WhenOneIsRevoked()
    {
        // Arrange
        var userId = "1";
        var token1 = Guid.NewGuid().ToString();
        var token2 = Guid.NewGuid().ToString();
        var expiration = DateTime.UtcNow.AddDays(7);

        await _store.StoreAsync(userId, token1, expiration);
        await _store.StoreAsync(userId, token2, expiration);

        // Act
        await _store.RevokeAsync(token1);
        var result1 = await _store.ValidateAsync(token1);
        var result2 = await _store.ValidateAsync(token2);

        // Assert
        Assert.Null(result1);
        Assert.Equal(userId, result2);
    }

    #endregion
}