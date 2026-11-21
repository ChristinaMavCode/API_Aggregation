using Microsoft.Extensions.Logging;
using Moq;
using StartUp.Application.Locking;
using StartUp.Domain;

namespace StartUp.UnitTests;

public class EntityLockServiceTests
{
    private readonly Mock<IEntityLocksRepository> _mockRepository;
    private readonly Mock<ILogger<EntityLocksService>> _mockLogger;
    private readonly EntityLocksService _service;

    public EntityLockServiceTests()
    {
        _mockRepository = new Mock<IEntityLocksRepository>();
        _mockLogger = new Mock<ILogger<EntityLocksService>>();

        _service = new EntityLocksService(_mockRepository.Object, _mockLogger.Object);
    }

    #region AcquireLock Tests

    [Fact]
    public async Task AcquireLock_ThrowsArgumentException_When_EntityTypeIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AcquireLock(null, "id1", "user1", "John", "conn1"));
    }

    [Fact]
    public async Task AcquireLock_ThrowsArgumentException_When_EntityIdIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AcquireLock("Order", null, "user1", "John", "conn1"));
    }

    [Fact]
    public async Task AcquireLock_ReturnsSuccess_When_NoExistingLock()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetActiveLockAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((EntityLock)null);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<EntityLock>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.AcquireLock("Order", "id1", "user1", "John", "conn1");

        // Assert
        Assert.True(result.Success);
        Assert.False(result.IsLocked);
        Assert.Equal("Lock acquired successfully", result.Message);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<EntityLock>()), Times.Once);
    }

    [Fact]
    public async Task AcquireLock_ReturnsFailed_When_LockedByAnotherUser()
    {
        // Arrange
        var existingLock = new EntityLock
        {
            Id = 1,
            EntityType = "Order",
            EntityId = "id1",
            UserId = "user2",
            UserName = "Jane",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            LockedAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow,
            ConnectionId = "conn2"
        };

        _mockRepository.Setup(r => r.GetActiveLockAsync("Order", "id1"))
            .ReturnsAsync(existingLock);

        // Act
        var result = await _service.AcquireLock("Order", "id1", "user1", "John", "conn1");

        // Assert
        Assert.False(result.Success);
        Assert.True(result.IsLocked);
        Assert.Equal("Jane", result.LockedBy);
        Assert.Contains("Jane", result.Message);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<EntityLock>()), Times.Never);
    }

    [Fact]
    public async Task AcquireLock_ReturnsSuccess_When_UserRenewsOwnLock()
    {
        // Arrange
        var existingLock = new EntityLock
        {
            Id = 1,
            EntityType = "Order",
            EntityId = "id1",
            UserId = "user1",
            UserName = "John",
            ExpiresAt = DateTime.UtcNow.AddMinutes(2),
            LockedAt = DateTime.UtcNow.AddMinutes(-3),
            LastHeartbeat = DateTime.UtcNow,
            ConnectionId = "conn1"
        };

        _mockRepository.Setup(r => r.GetActiveLockAsync("Order", "id1"))
            .ReturnsAsync(existingLock);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<EntityLock>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.AcquireLock("Order", "id1", "user1", "John", "conn1");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Lock renewed", result.Message);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<EntityLock>()), Times.Once);
    }

    #endregion

    #region ExtendLock Tests

    [Fact]
    public async Task ExtendLock_ReturnsFalse_When_LockNotFound()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((EntityLock)null);

        // Act
        var result = await _service.ExtendLock(1, "conn1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExtendLock_ReturnsFalse_When_ConnectionIdMismatch()
    {
        // Arrange
        var lockEntity = new EntityLock
        {
            Id = 1,
            ConnectionId = "conn1",
            ExpiresAt = DateTime.UtcNow.AddMinutes(2)
        };

        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(lockEntity);

        // Act
        var result = await _service.ExtendLock(1, "conn2");

        // Assert
        Assert.False(result);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<EntityLock>()), Times.Never);
    }

    [Fact]
    public async Task ExtendLock_DeletesLock_When_Expired()
    {
        // Arrange
        var lockEntity = new EntityLock
        {
            Id = 1,
            ConnectionId = "conn1",
            ExpiresAt = DateTime.UtcNow.AddSeconds(-10)
        };

        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(lockEntity);
        _mockRepository.Setup(r => r.DeleteAsync(It.IsAny<EntityLock>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ExtendLock(1, "conn1");

        // Assert
        Assert.False(result);
        _mockRepository.Verify(r => r.DeleteAsync(lockEntity), Times.Once);
    }

    [Fact]
    public async Task ExtendLock_ReturnsTrue_When_ValidExtension()
    {
        // Arrange
        var lockEntity = new EntityLock
        {
            Id = 1,
            ConnectionId = "conn1",
            ExpiresAt = DateTime.UtcNow.AddMinutes(2),
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-5)
        };

        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(lockEntity);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<EntityLock>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ExtendLock(1, "conn1");

        // Assert
        Assert.True(result);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<EntityLock>()), Times.Once);
    }

    #endregion

    #region ReleaseLock Tests

    [Fact]
    public async Task ReleaseLock_DeletesLock_When_Found()
    {
        // Arrange
        var lockEntity = new EntityLock
        {
            Id = 1,
            EntityType = "Order",
            EntityId = "id1",
            UserName = "John"
        };

        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(lockEntity);
        _mockRepository.Setup(r => r.DeleteAsync(It.IsAny<EntityLock>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.ReleaseLock(1);

        // Assert
        _mockRepository.Verify(r => r.DeleteAsync(lockEntity), Times.Once);
    }

    [Fact]
    public async Task ReleaseLock_DoesNotThrow_When_LockNotFound()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((EntityLock)null);

        // Act & Assert
        await _service.ReleaseLock(1); // Should not throw

        _mockRepository.Verify(r => r.DeleteAsync(It.IsAny<EntityLock>()), Times.Never);
    }

    #endregion

    #region ReleaseByConnectionId Tests

    [Fact]
    public async Task ReleaseByConnectionId_DeletesAllLocksForConnection()
    {
        // Arrange
        var locks = new List<EntityLock>
        {
            new EntityLock { Id = 1, EntityType = "Order", EntityId = "id1", UserName = "John", ConnectionId = "conn1" },
            new EntityLock { Id = 2, EntityType = "Product", EntityId = "id2", UserName = "John", ConnectionId = "conn1" }
        };

        _mockRepository.Setup(r => r.GetByConnectionIdAsync("conn1"))
            .ReturnsAsync(locks);
        _mockRepository.Setup(r => r.DeleteRangeAsync(It.IsAny<List<EntityLock>>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.ReleaseByConnectionId("conn1");

        // Assert
        _mockRepository.Verify(r => r.DeleteRangeAsync(locks), Times.Once);
    }

    #endregion

    #region GetLockStatus Tests

    [Fact]
    public async Task GetLockStatus_ReturnsNotLocked_When_NoActiveLock()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetActiveLockAsync("Order", "id1"))
            .ReturnsAsync((EntityLock)null);

        // Act
        var result = await _service.GetLockStatus("Order", "id1");

        // Assert
        Assert.True(result.Success);
        Assert.False(result.IsLocked);
    }

    [Fact]
    public async Task GetLockStatus_ReturnsLockDetails_When_Locked()
    {
        // Arrange
        var lockEntity = new EntityLock
        {
            Id = 1,
            UserName = "Jane",
            ExpiresAt = DateTime.UtcNow.AddMinutes(3)
        };

        _mockRepository.Setup(r => r.GetActiveLockAsync("Order", "id1"))
            .ReturnsAsync(lockEntity);

        // Act
        var result = await _service.GetLockStatus("Order", "id1");

        // Assert
        Assert.False(result.Success);
        Assert.True(result.IsLocked);
        Assert.Equal("Jane", result.LockedBy);
        Assert.True(result.TimeRemaining.HasValue);
        Assert.True(result.TimeRemaining.Value.TotalMinutes > 2);
    }

    #endregion

    #region GetUserLocks Tests

    [Fact]
    public async Task GetUserLocks_ReturnsEmptyList_When_NoLocksForUser()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByUserIdAsync("user1"))
            .ReturnsAsync(new List<EntityLock>());

        // Act
        var result = await _service.GetUserLocks("user1");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUserLocks_ReturnsUserLocks()
    {
        // Arrange
        var locks = new List<EntityLock>
        {
            new EntityLock { Id = 1, EntityType = "Order", EntityId = "id1" },
            new EntityLock { Id = 2, EntityType = "Product", EntityId = "id2" }
        };

        _mockRepository.Setup(r => r.GetByUserIdAsync("user1"))
            .ReturnsAsync(locks);

        // Act
        var result = await _service.GetUserLocks("user1");

        // Assert
        Assert.Equal(2, result.Count);
    }

    #endregion
}