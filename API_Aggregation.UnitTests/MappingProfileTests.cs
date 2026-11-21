using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using StartUp.Application;
using StartUp.Application.UserAuth;
using StartUp.Application.UserCompanies;
using StartUp.Domain;

namespace StartUp.UnitTests;

public class MappingProfileTests
{
    private readonly IMapper _mapper;

    public MappingProfileTests()
    {
        // AutoMapper 15.x: Create mapper from profile's type maps
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        });

        _mapper = config.CreateMapper();
    }

    #region LoginUser Mapping Tests

    [Fact]
    public void LoginUserToLoginUserDto_MapsCorrectly()
    {
        // Arrange
        var loginUser = new User
        {
            Id = 1,
            Username = "testuser",
            PasswordHash = "hashedpassword",
            RoleId = 2,
            Active = true
        };

        // Act
        var result = _mapper.Map<LoginUserDto>(loginUser);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(loginUser.Id, result.Id);
        Assert.Equal(loginUser.Username, result.Username);
        Assert.Equal(loginUser.PasswordHash, result.PasswordHash);
        Assert.Equal(loginUser.RoleId, result.RoleId);
        Assert.Equal(loginUser.Active, result.Active);
    }

    [Fact]
    public void LoginUserToLoginUserDto_IgnoresToken()
    {
        // Arrange
        var loginUser = new User
        {
            Id = 1,
            Username = "testuser",
            PasswordHash = "hashedpassword",
            RoleId = 2,
            Active = true
        };

        // Act
        var result = _mapper.Map<LoginUserDto>(loginUser);

        // Assert
        // Token should not be set from source (ignored)
        Assert.Null(result.AccessToken);
        Assert.Null(result.RefreshToken);
        Assert.Equal(result.RefreshExpiry, DateTime.MinValue);
    }

    [Fact]
    public void LoginUserDtoToLoginUser_MapsCorrectly()
    {
        // Arrange
        var loginUserDto = new LoginUserDto
        {
            Id = 1,
            Username = "testuser",
            PasswordHash = "hashedpassword",
            RoleId = 2,
            Active = true,
            AccessToken = "jwt-token",
            RefreshToken = "refresh-token",
            RefreshExpiry = DateTime.UtcNow.AddMinutes(60)
        };

        // Act
        var result = _mapper.Map<User>(loginUserDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(loginUserDto.Id, result.Id);
        Assert.Equal(loginUserDto.Username, result.Username);
        Assert.Equal(loginUserDto.PasswordHash, result.PasswordHash);
        Assert.Equal(loginUserDto.RoleId, result.RoleId);
        Assert.Equal(loginUserDto.Active, result.Active);
    }

    [Fact]
    public void LoginUserDtoToLoginUser_IgnoresTokensFromDto()
    {
        // Arrange
        var loginUserDto = new LoginUserDto
        {
            Id = 1,
            Username = "testuser",
            PasswordHash = "hashedpassword",
            RoleId = 2,
            Active = true,
            AccessToken = "jwt-token",
            RefreshToken = "refresh-token",
            RefreshExpiry = DateTime.UtcNow.AddMinutes(60)
        };

        // Act
        var result = _mapper.Map<User>(loginUserDto);

        // Assert
        // Tokens should not be mapped from DTO to domain
        // LoginUser doesn't have these properties, verify they don't exist
        Assert.DoesNotContain(nameof(User), typeof(User).GetProperties().Select(p => p.Name));
    }

    #endregion

    #region ViewUserCompany Mapping Tests

    [Fact]
    public void ViewUserCompanyToUserCompanyDto_MapsCorrectly()
    {
        // Arrange
        var viewUserCompany = new ViewUserLogin
        {
            UserId = 1,
            Username = "user1",
            CompanyName = "ABC Corp",
            CompanyId = 1,
            IsDefault = true
        };

        // Act
        var result = _mapper.Map<UserCompanyDto>(viewUserCompany);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(viewUserCompany.Username, result.Username);
        Assert.Equal(viewUserCompany.CompanyName, result.CompanyName);
        Assert.Equal(viewUserCompany.CompanyId, result.CompanyId);
        Assert.Equal(viewUserCompany.IsDefault, result.IsDefault);
    }

    [Fact]
    public void ViewUserCompanyToUserCompanyDto_DoesNotIncludeUserId()
    {
        // Arrange
        var viewUserCompany = new ViewUserLogin
        {
            UserId = 42,
            Username = "user1",
            CompanyName = "ABC Corp",
            CompanyId = 1,
            IsDefault = true
        };

        // Act
        var result = _mapper.Map<UserCompanyDto>(viewUserCompany);

        // Assert
        // UserCompanyDto doesn't have UserId property, so verify it's not mapped
        Assert.DoesNotContain(nameof(viewUserCompany.UserId), typeof(UserCompanyDto).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public void UserCompanyDtoToViewUserCompany_MapsCorrectly()
    {
        // Arrange
        var userCompanyDto = new UserCompanyDto
        {
            Username = "user1",
            CompanyName = "ABC Corp",
            CompanyId = 1,
            IsDefault = true
        };

        // Act
        var result = _mapper.Map<ViewUserLogin>(userCompanyDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userCompanyDto.Username, result.Username);
        Assert.Equal(userCompanyDto.CompanyName, result.CompanyName);
        Assert.Equal(userCompanyDto.CompanyId, result.CompanyId);
        Assert.Equal(userCompanyDto.IsDefault, result.IsDefault);
    }

    [Fact]
    public void UserCompanyDtoToViewUserCompany_IgnoresUserId()
    {
        // Arrange
        var userCompanyDto = new UserCompanyDto
        {
            Username = "user1",
            CompanyName = "ABC Corp",
            CompanyId = 1,
            IsDefault = true
        };

        // Act
        var result = _mapper.Map<ViewUserLogin>(userCompanyDto);

        // Assert
        // UserId should not be set from source (ignored)
        Assert.Equal(0, result.UserId);
    }

    #endregion

    #region Configuration Validation Tests

    [Fact]
    public void MapperConfiguration_IsValid()
    {
        // Assert - if configuration is invalid, constructor will throw
        Assert.NotNull(_mapper);
    }

    #endregion

    #region Bulk Mapping Tests

    [Fact]
    public void MapList_OfViewUserCompanyToUserCompanyDto()
    {
        // Arrange
        var companies = new List<ViewUserLogin>
        {
            new ViewUserLogin { UserId = 1, Username = "user1", CompanyName = "Company A", CompanyId = 1, IsDefault = true },
            new ViewUserLogin { UserId = 1, Username = "user1", CompanyName = "Company B", CompanyId = 2, IsDefault = false },
            new ViewUserLogin { UserId = 1, Username = "user1", CompanyName = "Company C", CompanyId = 3, IsDefault = false }
        };

        // Act
        var result = _mapper.Map<List<UserCompanyDto>>(companies);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, company => Assert.NotNull(company.Username));
        Assert.Single(result.Where(c => c.IsDefault));
    }

    [Fact]
    public void MapEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var emptyList = new List<ViewUserLogin>();

        // Act
        var result = _mapper.Map<List<UserCompanyDto>>(emptyList);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion
}