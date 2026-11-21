using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using StartUp.Application.UserAuth;
using StartUp.Domain;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TMC_ERP_Common.Application.Login;
using TMC_ERP_Common.Domain;

namespace StartUp.UnitTests;

public class LoginServiceTests
{
    private readonly Mock<ILoginRepository> _mockLoginRepository;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IMapper> _mockMapper;
    private readonly AuthService _authService;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IRefreshTokenStore> _mockRefreshTokenStore;
    private readonly Mock<ILogger<AuthService>> _mockLogger;

    public LoginServiceTests()
    {
        _mockLoginRepository = new Mock<ILoginRepository>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockMapper = new Mock<IMapper>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockRefreshTokenStore = new Mock<IRefreshTokenStore>();
        _mockLogger = new Mock<ILogger<AuthService>>();

        // Setup mapper
        _mockMapper
            .Setup(m => m.Map<LoginUserDto>(It.IsAny<User>()))
            .Returns((User src) => new LoginUserDto
            {
                Id = src.Id,
                Username = src.Username,
                PasswordHash = src.PasswordHash,
                RoleId = src.RoleId,
                Active = src.Active
            });
        _mockMapper
            .Setup(m => m.Map<LoginUserDto>(It.IsAny<ViewUserLogin>()))
            .Returns((ViewUserLogin src) => new LoginUserDto
            {
                Id = src.UserId,
                Username = src.Username,
                PasswordHash = src.PasswordHash,
                RoleId = src.RoleId,
                Active = src.Active
            });

        // Setup configuration
        _mockConfiguration.Setup(c => c["Jwt:Key"])
            .Returns("ThisIsASuperSecretKeyThatIsAtLeast32CharactersLong!");
        _mockConfiguration.Setup(c => c["Jwt:Issuer"])
            .Returns("TMC-ERP-Auth");
        _mockConfiguration.Setup(c => c["Jwt:Audience"])
            .Returns("TMC-ERP-Services");

        _authService = new AuthService(
            _mockLoginRepository.Object,
            _mockMapper.Object,
            _mockConfiguration.Object,
            _mockCurrentUserService.Object,
            _mockRefreshTokenStore.Object,
            _mockLogger.Object
        );
    }

    #region Login Tests

    [Fact]
    public async Task Login_ReturnsUserWithToken_When_CredentialsAreValid()
    {
        // Arrange
        var username = "user1";
        var password = "pass123";
        var userId = 42;
        byte roleId = 1;
        byte companyId = 1;
        double expiryMinutes = 60;
        double refreshExpiryMinutes = 1440;

        var expectedUser = new ViewUserLogin
        {
            CompanyId = companyId,
            CompanyName = "Test Company",
            UserId = userId,
            Username = username,
            RoleId = roleId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Active = true
        };

        _mockLoginRepository
            .Setup(r => r.GetValidUserAsync(username, password, companyId))
            .ReturnsAsync(ActionResult<ViewUserLogin>.Yes(expectedUser));

        _mockRefreshTokenStore
            .Setup(r => r.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.Login(new LoginRequestDto { Username = username, Password = password, CompanyId = companyId, languageCode = "en-US" }, expiryMinutes, refreshExpiryMinutes);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Entity);
        Assert.Equal(username, result.Entity.Username);
        Assert.Equal(userId, result.Entity.Id);
        Assert.NotNull(result.Entity.AccessToken);
        Assert.NotNull(result.Entity.RefreshToken);

        // Verify JWT token
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(result.Entity.AccessToken) as JwtSecurityToken;

        Assert.NotNull(jwtToken);
        Assert.Equal("TMC-ERP-Auth", jwtToken.Issuer);
        Assert.Contains("TMC-ERP-Services", jwtToken.Audiences);

        // Verify claims
        var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        Assert.NotNull(userIdClaim);
        Assert.Equal(userId.ToString(), userIdClaim.Value);

        var usernameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
        Assert.NotNull(usernameClaim);
        Assert.Equal(username, usernameClaim.Value);

        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        Assert.NotNull(roleClaim);
        Assert.Equal(roleId.ToString(), roleClaim.Value);

        _mockLoginRepository.Verify(r => r.GetValidUserAsync(username, password, companyId), Times.Once);
        _mockRefreshTokenStore.Verify(r => r.StoreAsync(userId.ToString(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task Login_ReturnsNull_When_UserDoesNotExist()
    {
        // Arrange
        byte companyId = 1;
        var username = "nonexistent";
        var password = "anypassword";
        double expiryMinutes = 60;
        double refreshExpiryMinutes = 1440;

        _mockLoginRepository
            .Setup(r => r.GetValidUserAsync(username, password, companyId))
            .ReturnsAsync(ActionResult<ViewUserLogin>.No("nonexistentusername"));

        // Act
        var result = await _authService.Login(new LoginRequestDto { Username = username, Password = password, CompanyId = companyId, languageCode = "en-US" }, expiryMinutes, refreshExpiryMinutes);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Entity);
    }

    [Fact]
    public async Task Login_ReturnsNull_When_PasswordIsIncorrect()
    {
        // Arrange
        byte companyId = 1;
        var username = "user1";
        var incorrectPassword = "wrongpass";
        double expiryMinutes = 60;
        double refreshExpiryMinutes = 1440;

        _mockLoginRepository
            .Setup(r => r.GetValidUserAsync(username, incorrectPassword, companyId))
            .ReturnsAsync(ActionResult<ViewUserLogin>.No("wrongpass"));

        // Act
        var result = await _authService.Login(new LoginRequestDto { Username = username, Password = incorrectPassword, CompanyId = companyId, languageCode = "en-US" }, expiryMinutes, refreshExpiryMinutes);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Entity);
    }

    public static TheoryData<string?, string> InvalidUsernames => new()
    {
        { null,  "password" },
        { "",    "password" },
        { "   ", "password" }
    };

    [Theory]
    //[InlineData(null, "password")]
    //[InlineData("", "password")]
    //[InlineData("   ", "password")]
    [MemberData(nameof(InvalidUsernames))]
    public async Task Login_ThrowsArgumentException_When_UsernameIsNullOrEmpty(string? invalidUsername, string password)
    {
        // Arrange
        double expiryMinutes = 60;
        double refreshExpiryMinutes = 1440;

        var result = await _authService.Login(new LoginRequestDto { Username = invalidUsername, Password = password, CompanyId = 1, languageCode = "en-US" }, expiryMinutes, refreshExpiryMinutes);

        // Act & Assert
        Assert.False(result.Success);
        Assert.Null(result.Entity);

        _mockLoginRepository.Verify(r => r.GetValidUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte>()), Times.Never);
    }

    public static TheoryData<string, string?> InvalidPasswords => new()
    {
        { "username", null  },
        { "username", ""    },
        { "username", "   " }
    };

    [Theory]
    //[InlineData("username", null)]
    //[InlineData("username", "")]
    //[InlineData("username", "   ")]
    [MemberData(nameof(InvalidPasswords))]
    public async Task Login_Fails_When_PasswordIsNullOrEmpty(string username, string? invalidPassword)
    {
        // Arrange
        double expiryMinutes = 60;
        double refreshExpiryMinutes = 1440;

        var result = await _authService.Login(new LoginRequestDto { Username = username, Password = invalidPassword, CompanyId = 1, languageCode = "en-US" }, expiryMinutes, refreshExpiryMinutes);

        // Act & Assert
        Assert.False(result.Success);
        Assert.Null(result.Entity);

        _mockLoginRepository.Verify(r => r.GetValidUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte>()), Times.Never);
    }

    [Fact]
    public async Task Login_TrimsWhitespace_From_Username()
    {
        // Arrange
        byte companyId = 1;
        var usernameWithSpaces = "  user1  ";
        var trimmedUsername = "user1";
        var password = "pass123";
        double expiryMinutes = 60;
        double refreshExpiryMinutes = 1440;

        var expectedUser = new ViewUserLogin
        {
            CompanyId = companyId,           
            CompanyName = "Test Company",
            UserId = 1,
            Username = trimmedUsername,
            RoleId = 1,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Active = true
        };

        _mockLoginRepository
            .Setup(r => r.GetValidUserAsync(trimmedUsername, password, companyId))
            .ReturnsAsync(ActionResult<ViewUserLogin>.Yes(expectedUser));

        _mockRefreshTokenStore
            .Setup(r => r.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.Login(new LoginRequestDto { Username = usernameWithSpaces, Password = password, CompanyId = companyId, languageCode = "en-US" }, expiryMinutes, refreshExpiryMinutes);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Entity);
        _mockLoginRepository.Verify(r => r.GetValidUserAsync(trimmedUsername, password, companyId), Times.Once);
    }

    [Fact]
    public async Task Login_GeneratesValidJwtToken()
    {
        // Arrange
        byte companyId = 1;
        var username = "user1";
        var password = "pass123";
        var userId = 1;
        var roleId = (byte)1;
        double expiryMinutes = 60;
        double refreshExpiryMinutes = 1440;

        var expectedUser = new ViewUserLogin
        {
            CompanyId = companyId,
            CompanyName = "Test Company",
            UserId = userId,
            Username = username,
            RoleId = roleId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Active = true
        };

        _mockLoginRepository
            .Setup(r => r.GetValidUserAsync(username, password, companyId))
            .ReturnsAsync(ActionResult<ViewUserLogin>.Yes(expectedUser));

        _mockRefreshTokenStore
            .Setup(r => r.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.Login(new LoginRequestDto { Username = username, Password = password, CompanyId = companyId, languageCode = "en-US" }, expiryMinutes, refreshExpiryMinutes);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Entity);
        Assert.NotNull(result.Entity.AccessToken);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(result.Entity.AccessToken) as JwtSecurityToken;

        Assert.NotNull(jwtToken);
        Assert.True(jwtToken.ValidFrom <= DateTime.UtcNow);
        Assert.True(jwtToken.ValidTo >= DateTime.UtcNow);
    }

    #endregion

    #region RefreshUserToken Tests

    [Fact]
    public async Task RefreshUserToken_ReturnsUserWithNewToken_When_UserExists()
    {
        // Arrange
        var userId = 42;
        var username = "user1";
        var roleId = (byte)1;
        var refreshToken = Guid.NewGuid().ToString();
        double expiryMinutes = 60;
        double refreshExpiryMinutes = 1440;

        var expectedUser = new User
        {
            Id = userId,
            Username = username,
            RoleId = roleId,
            PasswordHash = "somehash",
            Active = true
        };

        var request = new RefreshTokenRequest(refreshToken);

        _mockCurrentUserService.Setup(s => s.Language).Returns("en-US");

        _mockRefreshTokenStore
            .Setup(r => r.ValidateAsync(refreshToken))
            .ReturnsAsync(userId.ToString());

        _mockLoginRepository
            .Setup(r => r.GetUserByIdAsync(userId))
            .ReturnsAsync(expectedUser);

        _mockRefreshTokenStore
            .Setup(r => r.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        _mockRefreshTokenStore
            .Setup(r => r.RevokeAsync(refreshToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.RefreshUserToken(request, expiryMinutes, refreshExpiryMinutes);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Entity);
        Assert.Equal(username, result.Entity.Username);
        Assert.Equal(userId, result.Entity.Id);
        Assert.NotNull(result.Entity.AccessToken);
        Assert.NotNull(result.Entity.RefreshToken);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(result.Entity.AccessToken) as JwtSecurityToken;

        Assert.NotNull(jwtToken);
        Assert.Equal("TMC-ERP-Auth", jwtToken.Issuer);

        _mockLoginRepository.Verify(r => r.GetUserByIdAsync(userId), Times.Once);
        _mockRefreshTokenStore.Verify(r => r.ValidateAsync(refreshToken), Times.Once);
        _mockRefreshTokenStore.Verify(r => r.RevokeAsync(refreshToken), Times.Once);
    }

    [Fact]
    public async Task RefreshUserToken_ReturnsNull_When_UserDoesNotExist()
    {
        // Arrange
        var userId = 999;
        var refreshToken = Guid.NewGuid().ToString();
        double expiryMinutes = 60;
        double refreshExpiryMinutes = 1440;

        var request = new RefreshTokenRequest(refreshToken);

        _mockCurrentUserService.Setup(s => s.Language).Returns("en-US");

        _mockRefreshTokenStore
            .Setup(r => r.ValidateAsync(refreshToken))
            .ReturnsAsync(userId.ToString());

        _mockLoginRepository
            .Setup(r => r.GetUserByIdAsync(userId))
            .ReturnsAsync((User)null);

        // Act
        var result = await _authService.RefreshUserToken(request, expiryMinutes, refreshExpiryMinutes);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Entity);
    }

    [Fact]
    public async Task RefreshUserToken_ReturnsNull_When_RefreshTokenIsInvalid()
    {
        // Arrange
        var refreshToken = Guid.NewGuid().ToString();
        double expiryMinutes = 60;
        double refreshExpiryMinutes = 1440;

        var request = new RefreshTokenRequest(refreshToken);

        _mockCurrentUserService.Setup(s => s.Language).Returns("en-US");

        _mockRefreshTokenStore
            .Setup(r => r.ValidateAsync(refreshToken))
            .ReturnsAsync((string)null);

        // Act
        var result = await _authService.RefreshUserToken(request, expiryMinutes, refreshExpiryMinutes);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Entity);
    }

    [Fact]
    public async Task RefreshUserToken_GeneratesNewToken_WithUpdatedExpiration()
    {
        // Arrange
        var userId = 1;
        var username = "user1";
        var roleId = (byte)1;
        var refreshToken = Guid.NewGuid().ToString();
        double expiryMinutes = 120; // 2 hours
        double refreshExpiryMinutes = 1440;

        var expectedUser = new User
        {
            Id = userId,
            Username = username,
            RoleId = roleId,
            PasswordHash = "somehash",
            Active = true
        };

        var request = new RefreshTokenRequest(refreshToken);

        _mockCurrentUserService.Setup(s => s.Language).Returns("en-US");

        _mockRefreshTokenStore
            .Setup(r => r.ValidateAsync(refreshToken))
            .ReturnsAsync(userId.ToString());

        _mockLoginRepository
            .Setup(r => r.GetUserByIdAsync(userId))
            .ReturnsAsync(expectedUser);

        _mockRefreshTokenStore
            .Setup(r => r.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        _mockRefreshTokenStore
            .Setup(r => r.RevokeAsync(refreshToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.RefreshUserToken(request, expiryMinutes, refreshExpiryMinutes);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Entity);
        Assert.NotNull(result?.Entity.AccessToken);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(result.Entity.AccessToken) as JwtSecurityToken;

        Assert.NotNull(jwtToken);
        // Token expiration should be close to expiryMinutes from now
        var expectedExpiration = DateTime.UtcNow.AddMinutes(expiryMinutes);
        Assert.True(Math.Abs((jwtToken.ValidTo - expectedExpiration).TotalSeconds) < 5);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Login_HandlesRepositoryException_Gracefully()
    {
        // Arrange
        byte companyId = 1;
        var username = "user1";
        var password = "pass123";
        double expiryMinutes = 60;
        double refreshExpiryMinutes = 1440;

        _mockLoginRepository
            .Setup(r => r.GetValidUserAsync(username, password, companyId))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _authService.Login(new LoginRequestDto { Username = username, Password = password, CompanyId = companyId, languageCode = "en-US" }, expiryMinutes, refreshExpiryMinutes);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Entity);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task Login_DoesNotCacheResults()
    {
        // Arrange
        byte companyId = 1;
        var username = "user1";
        var password = "pass123";
        double expiryMinutes = 60;
        double refreshExpiryMinutes = 1440;

        var expectedUser = new ViewUserLogin
        {
            CompanyId = companyId,
            CompanyName = "Test Company",
            UserId = 1,
            Username = username,
            RoleId = 1,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Active = true
        };

        _mockLoginRepository
            .SetupSequence(r => r.GetValidUserAsync(username, password, companyId))
            .ReturnsAsync(ActionResult<ViewUserLogin>.Yes(expectedUser))
            .ReturnsAsync(ActionResult<ViewUserLogin>.No("NoCash"));

        _mockRefreshTokenStore
            .Setup(r => r.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        // Act
        var firstResult = await _authService.Login(new LoginRequestDto { Username = username, Password = password, CompanyId = companyId, languageCode = "en-US" }, expiryMinutes, refreshExpiryMinutes);
        var secondResult = await _authService.Login(new LoginRequestDto { Username = username, Password = password, CompanyId = companyId, languageCode = "en-US" }, expiryMinutes, refreshExpiryMinutes);

        // Assert
        Assert.True(firstResult.Success);
        Assert.NotNull(firstResult.Entity);

        Assert.False(secondResult.Success);
        Assert.Null(secondResult.Entity);

        _mockLoginRepository.Verify(r => r.GetValidUserAsync(username, password, companyId), Times.Exactly(2));
    }

    [Fact]
    public async Task Login_ThrowsInvalidOperationException_When_JwtKeyNotConfigured()
    {
        // Arrange
        byte companyId = 1;
        var username = "user1";
        var password = "pass123";
        double expiryMinutes = 60;
        double refreshExpiryMinutes = 1440;

        var mockConfigNullKey = new Mock<IConfiguration>();
        mockConfigNullKey.Setup(c => c["Jwt:Key"]).Returns((string)null);
        mockConfigNullKey.Setup(c => c["Jwt:Issuer"]).Returns("TMC-ERP-Auth");
        mockConfigNullKey.Setup(c => c["Jwt:Audience"]).Returns("TMC-ERP-Services");

        var authService = new AuthService(_mockLoginRepository.Object, 
            _mockMapper.Object,
            mockConfigNullKey.Object, 
            _mockCurrentUserService.Object, 
            _mockRefreshTokenStore.Object, 
            _mockLogger.Object);

        var expectedUser = new ViewUserLogin
        {
            CompanyId = companyId,
            CompanyName = "Test Company",
            UserId = 1,
            Username = username,
            RoleId = 1,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Active = true
        };

        _mockLoginRepository
            .Setup(r => r.GetValidUserAsync(username, password, companyId))
            .ReturnsAsync(ActionResult<ViewUserLogin>.Yes(expectedUser));

        // Act & Assert
        var result = await authService.Login(new LoginRequestDto { Username = username, Password = password, CompanyId = companyId, languageCode = "en-US" }, expiryMinutes, refreshExpiryMinutes);
        
        Assert.False(result.Success);
        Assert.Null(result.Entity);
    }

    #endregion

    #region Token Content Tests

    [Fact]
    public async Task Login_TokenIncludesAllRequiredClaims()
    {
        // Arrange
        byte companyId = 1;
        var username = "user1";
        var password = "pass123";
        var userId = 42;
        var roleId = (byte)5;
        double expiryMinutes = 60;
        double refreshExpiryMinutes = 1440;

        var expectedUser = new ViewUserLogin
        {
            CompanyId = companyId,
            CompanyName = "Test Company",
            UserId = userId,
            Username = username,
            RoleId = roleId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Active = true
        };

        _mockLoginRepository
            .Setup(r => r.GetValidUserAsync(username, password, companyId))
            .ReturnsAsync(ActionResult<ViewUserLogin>.Yes(expectedUser));

        _mockRefreshTokenStore
            .Setup(r => r.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.Login(new LoginRequestDto { Username = username, Password = password, CompanyId = companyId, languageCode = "en-US" }, expiryMinutes, refreshExpiryMinutes);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(result.Entity.AccessToken) as JwtSecurityToken;

        var claims = jwtToken.Claims.ToList();

        // Verify all required claims are present
        Assert.NotNull(claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier));
        Assert.NotNull(claims.FirstOrDefault(c => c.Type == ClaimTypes.Name));
        Assert.NotNull(claims.FirstOrDefault(c => c.Type == ClaimTypes.Role));
        Assert.NotNull(claims.FirstOrDefault(c => c.Type == "sub"));

        // Verify claim values
        Assert.Equal(userId.ToString(), claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(username, claims.First(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal(roleId.ToString(), claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    #endregion
}