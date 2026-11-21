using StartUp.Application.CQRS.Queries;
using Moq;
using StartUp.Application.UserCompanies;

namespace StartUp.UnitTests;

public class GetUserCompaniesQueryHandlerTests
{
    private readonly Mock<IUserCompaniesService> _mockService;
    private readonly GetUserCompaniesQueryHandler _handler;

    public GetUserCompaniesQueryHandlerTests()
    {
        _mockService = new Mock<IUserCompaniesService>();
        _handler = new GetUserCompaniesQueryHandler(_mockService.Object);
    }

    #region Success Tests

    [Fact]
    public async Task Handle_ReturnsCompanies_WhenServiceReturnsData()
    {
        // Arrange
        var expectedCompanies = new List<UserCompanyDto>
        {
            new UserCompanyDto
            {
                Username = "user1",
                CompanyName = "ABC Corp",
                CompanyId = 1,
                IsDefault = true
            },
            new UserCompanyDto
            {
                Username = "user1",
                CompanyName = "XYZ Ltd",
                CompanyId = 2,
                IsDefault = false
            }
        };

        _mockService
            .Setup(s => s.GetUserCompaniesAsync())
            .ReturnsAsync(expectedCompanies);

        var query = new GetUserCompaniesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("ABC Corp", result[0].CompanyName);
        Assert.Equal("XYZ Ltd", result[1].CompanyName);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenServiceReturnsEmptyList()
    {
        // Arrange
        _mockService
            .Setup(s => s.GetUserCompaniesAsync())
            .ReturnsAsync(new List<UserCompanyDto>());

        var query = new GetUserCompaniesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenServiceReturnsNull()
    {
        // Arrange
        _mockService
            .Setup(s => s.GetUserCompaniesAsync())
            .ReturnsAsync((List<UserCompanyDto>)null);

        var query = new GetUserCompaniesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_CallsServiceOnce()
    {
        // Arrange
        _mockService
            .Setup(s => s.GetUserCompaniesAsync())
            .ReturnsAsync(new List<UserCompanyDto>());

        var query = new GetUserCompaniesQuery();

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockService.Verify(s => s.GetUserCompaniesAsync(), Times.Once);
    }

    #endregion

    #region Data Integrity Tests

    [Fact]
    public async Task Handle_PreservesCompanyData()
    {
        // Arrange
        var company = new UserCompanyDto
        {
            Username = "testuser",
            CompanyName = "Test Corp",
            CompanyId = 99,
            IsDefault = true
        };

        _mockService
            .Setup(s => s.GetUserCompaniesAsync())
            .ReturnsAsync(new List<UserCompanyDto> { company });

        var query = new GetUserCompaniesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("testuser", result[0].Username);
        Assert.Equal("Test Corp", result[0].CompanyName);
        Assert.Equal(99, result[0].CompanyId);
        Assert.True(result[0].IsDefault);
    }

    [Fact]
    public async Task Handle_MaintainsOrderOfCompanies()
    {
        // Arrange
        var companies = new List<UserCompanyDto>
        {
            new UserCompanyDto { Username = "user1", CompanyName = "First", CompanyId = 1, IsDefault = false },
            new UserCompanyDto { Username = "user1", CompanyName = "Second", CompanyId = 2, IsDefault = false },
            new UserCompanyDto { Username = "user1", CompanyName = "Third", CompanyId = 3, IsDefault = false }
        };

        _mockService
            .Setup(s => s.GetUserCompaniesAsync())
            .ReturnsAsync(companies);

        var query = new GetUserCompaniesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("First", result[0].CompanyName);
        Assert.Equal("Second", result[1].CompanyName);
        Assert.Equal("Third", result[2].CompanyName);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task Handle_PropagatesException_WhenServiceThrows()
    {
        // Arrange
        _mockService
            .Setup(s => s.GetUserCompaniesAsync())
            .ThrowsAsync(new Exception("Service error"));

        var query = new GetUserCompaniesQuery();

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _handler.Handle(query, CancellationToken.None));
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task Handle_RespectsCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockService
            .Setup(s => s.GetUserCompaniesAsync())
            .ThrowsAsync(new OperationCanceledException());

        var query = new GetUserCompaniesQuery();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _handler.Handle(query, cts.Token));
    }

    #endregion
}