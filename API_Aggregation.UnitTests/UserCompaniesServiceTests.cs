using AutoMapper;
using Microsoft.Extensions.Logging;
using Moq;
using StartUp.Application.UserCompanies;
using StartUp.Domain;

namespace StartUp.UnitTests;

public class UserCompaniesServiceTests
{
    private readonly Mock<IUserCompaniesRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<ILogger<UserCompaniesService>> _mockLogger;
    private readonly Mock<IUserCompaniesCacheRepository> _mockCache;
    private readonly UserCompaniesService _service;

    public UserCompaniesServiceTests()
    {
        _mockRepository = new Mock<IUserCompaniesRepository>();
        _mockMapper = new Mock<IMapper>();
        _mockLogger = new Mock<ILogger<UserCompaniesService>>();
        _mockCache = new Mock<IUserCompaniesCacheRepository>();

        _service = new UserCompaniesService(
            _mockLogger.Object,
            _mockRepository.Object,
            _mockMapper.Object,
            _mockCache.Object);
    }

    #region Cache Hit Tests

    [Fact]
    public async Task GetUserCompaniesAsync_ReturnsCachedData_WhenCacheHit()
    {
        // Arrange
        var cachedCompanies = new List<UserCompanyDto>
        {
            new UserCompanyDto { Username = "user1", CompanyName = "Cached Company", CompanyId = 1, IsDefault = true }
        };

        _mockCache
            .Setup(c => c.GetAsync("user_companies_list"))
            .ReturnsAsync(cachedCompanies);

        // Act
        var result = await _service.GetUserCompaniesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Cached Company", result[0].CompanyName);
        
        // Verify repository was NOT called (cache hit)
        _mockRepository.Verify(r => r.GetUserCompanies(), Times.Never);
        // Verify cache was checked
        _mockCache.Verify(c => c.GetAsync("user_companies_list"), Times.Once);
    }

    [Fact]
    public async Task GetUserCompaniesAsync_FetchesFromRepository_WhenCacheMiss()
    {
        // Arrange
        var viewUserCompanies = new List<ViewUserLogin>
        {
            new ViewUserLogin { UserId = 1, Username = "user1", CompanyName = "Company A", CompanyId = 1, IsDefault = true },
            new ViewUserLogin { UserId = 1, Username = "user1", CompanyName = "Company B", CompanyId = 2, IsDefault = false }
        };

        _mockCache
            .Setup(c => c.GetAsync("user_companies_list"))
            .ReturnsAsync((List<UserCompanyDto>)null);

        _mockRepository
            .Setup(r => r.GetUserCompanies())
            .ReturnsAsync(viewUserCompanies);

        _mockMapper
            .Setup(m => m.Map<UserCompanyDto>(It.IsAny<ViewUserLogin>()))
            .Returns((ViewUserLogin src) => new UserCompanyDto
            {
                Username = src.Username,
                CompanyName = src.CompanyName,
                CompanyId = src.CompanyId,
                IsDefault = src.IsDefault
            });

        // Act
        var result = await _service.GetUserCompaniesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Company A", result[0].CompanyName);
        Assert.Equal("Company B", result[1].CompanyName);
        Assert.True(result[0].IsDefault);
        Assert.False(result[1].IsDefault);
        _mockRepository.Verify(r => r.GetUserCompanies(), Times.Once);
        _mockCache.Verify(c => c.SetAsync("user_companies_list", It.IsAny<List<UserCompanyDto>>()), Times.Once);
    }

    #endregion

    #region Successful Retrieval Tests

    [Fact]
    public async Task GetUserCompaniesAsync_ReturnsEmptyList_WhenNoCompaniesExist()
    {
        // Arrange
        var emptyList = new List<ViewUserLogin>();

        _mockCache
            .Setup(c => c.GetAsync("user_companies_list"))
            .ReturnsAsync((List<UserCompanyDto>)null);

        _mockRepository
            .Setup(r => r.GetUserCompanies())
            .ReturnsAsync(emptyList);

        // Act
        var result = await _service.GetUserCompaniesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        _mockRepository.Verify(r => r.GetUserCompanies(), Times.Once);
        // Cache should NOT be set for empty list
        _mockCache.Verify(c => c.SetAsync("user_companies_list", It.IsAny<List<UserCompanyDto>>()), Times.Never);
    }

    [Fact]
    public async Task GetUserCompaniesAsync_MapsAllCompanies_ToDto()
    {
        // Arrange
        var viewUserCompanies = new List<ViewUserLogin>
        {
            new ViewUserLogin { UserId = 1, Username = "user1", CompanyName = "ABC", CompanyId = 1, IsDefault = true },
            new ViewUserLogin { UserId = 1, Username = "user1", CompanyName = "XYZ", CompanyId = 2, IsDefault = false },
            new ViewUserLogin { UserId = 1, Username = "user1", CompanyName = "DEF", CompanyId = 3, IsDefault = false }
        };

        _mockCache
            .Setup(c => c.GetAsync("user_companies_list"))
            .ReturnsAsync((List<UserCompanyDto>)null);

        _mockRepository
            .Setup(r => r.GetUserCompanies())
            .ReturnsAsync(viewUserCompanies);

        _mockMapper
            .Setup(m => m.Map<UserCompanyDto>(It.IsAny<ViewUserLogin>()))
            .Returns((ViewUserLogin src) => new UserCompanyDto
            {
                Username = src.Username,
                CompanyName = src.CompanyName,
                CompanyId = src.CompanyId,
                IsDefault = src.IsDefault
            });

        // Act
        var result = await _service.GetUserCompaniesAsync();

        // Assert
        Assert.Equal(3, result.Count);
        _mockMapper.Verify(
            m => m.Map<UserCompanyDto>(It.IsAny<ViewUserLogin>()),
            Times.Exactly(3),
            "Mapper should be called for each company");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetUserCompaniesAsync_ReturnsNull_WhenRepositoryThrowsException()
    {
        // Arrange
        _mockCache
            .Setup(c => c.GetAsync("user_companies_list"))
            .ReturnsAsync((List<UserCompanyDto>)null);

        _mockRepository
            .Setup(r => r.GetUserCompanies())
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _service.GetUserCompaniesAsync();

        // Assert
        Assert.Null(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving companies")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserCompaniesAsync_LogsError_WhenExceptionOccurs()
    {
        // Arrange
        var testException = new InvalidOperationException("Test error");
        
        _mockCache
            .Setup(c => c.GetAsync("user_companies_list"))
            .ReturnsAsync((List<UserCompanyDto>)null);

        _mockRepository
            .Setup(r => r.GetUserCompanies())
            .ThrowsAsync(testException);

        // Act
        await _service.GetUserCompaniesAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                testException,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserCompaniesAsync_HandlesMapperException_Gracefully()
    {
        // Arrange
        var viewUserCompanies = new List<ViewUserLogin>
        {
            new ViewUserLogin { UserId = 1, Username = "user1", CompanyName = "Company A", CompanyId = 1, IsDefault = true }
        };

        _mockCache
            .Setup(c => c.GetAsync("user_companies_list"))
            .ReturnsAsync((List<UserCompanyDto>)null);

        _mockRepository
            .Setup(r => r.GetUserCompanies())
            .ReturnsAsync(viewUserCompanies);

        _mockMapper
            .Setup(m => m.Map<UserCompanyDto>(It.IsAny<ViewUserLogin>()))
            .Throws<AutoMapperMappingException>();

        // Act
        var result = await _service.GetUserCompaniesAsync();

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Data Integrity Tests

    [Fact]
    public async Task GetUserCompaniesAsync_PreservesCompanyData_InMapping()
    {
        // Arrange
        var testCompany = new ViewUserLogin
        {
            UserId = 42,
            Username = "testuser",
            CompanyName = "Test Corp",
            CompanyId = 99,
            IsDefault = true
        };

        _mockCache
            .Setup(c => c.GetAsync("user_companies_list"))
            .ReturnsAsync((List<UserCompanyDto>)null);

        _mockRepository
            .Setup(r => r.GetUserCompanies())
            .ReturnsAsync(new List<ViewUserLogin> { testCompany });

        _mockMapper
            .Setup(m => m.Map<UserCompanyDto>(It.IsAny<ViewUserLogin>()))
            .Returns((ViewUserLogin src) => new UserCompanyDto
            {
                Username = src.Username,
                CompanyName = src.CompanyName,
                CompanyId = src.CompanyId,
                IsDefault = src.IsDefault
            });

        // Act
        var result = await _service.GetUserCompaniesAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("testuser", result[0].Username);
        Assert.Equal("Test Corp", result[0].CompanyName);
        Assert.Equal(99, result[0].CompanyId);
        Assert.True(result[0].IsDefault);
    }

    #endregion

    #region Cache Invalidation Tests

    [Fact]
    public async Task InvalidateCacheAsync_CallsCacheRemove()
    {
        // Act
        await _service.InvalidateCacheAsync();

        // Assert
        _mockCache.Verify(c => c.RemoveAsync("user_companies_list"), Times.Once);
    }

    #endregion
}