using System.Text.Json;
using Moq;
using StackExchange.Redis;
using StartUp.Domain;
using StartUp.Infrastructure.Locking;

namespace StartUp.UnitTests;

public class RedisEntityLockRepositoryTests
{
    private readonly Mock<IDatabase> _mockDb;
    private readonly Mock<IConnectionMultiplexer> _mockMultiplexer;
    private readonly RedisEntityLocksRepository _repository;

    public RedisEntityLockRepositoryTests()
    {
        _mockDb = new Mock<IDatabase>();
        _mockMultiplexer = new Mock<IConnectionMultiplexer>();
        _mockMultiplexer
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDb.Object);

        _repository = new RedisEntityLocksRepository(_mockMultiplexer.Object);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static EntityLock MakeLock(string entityType = "Order", string entityId = "1",
        string userId = "user1", string userName = "John", string connectionId = "conn1") =>
        new EntityLock
        {
            Id = 1,
            EntityType = entityType,
            EntityId = entityId,
            UserId = userId,
            UserName = userName,
            ConnectionId = connectionId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            LockedAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow
        };

    private static RedisValue ToRedisJson(EntityLock entity) =>
        new RedisValue(JsonSerializer.Serialize(entity));

    // ── GetActiveLockAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveLockAsync_ReturnsNull_When_KeyDoesNotExist()
    {
        _mockDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(RedisValue.Null);

        var result = await _repository.GetActiveLockAsync("Order", "1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveLockAsync_ReturnsDeserializedLock_When_KeyExists()
    {
        var entity = MakeLock();
        _mockDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(ToRedisJson(entity));

        var result = await _repository.GetActiveLockAsync("Order", "1");

        Assert.NotNull(result);
        Assert.Equal("Order", result.EntityType);
        Assert.Equal("1", result.EntityId);
        Assert.Equal("John", result.UserName);
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_When_IdKeyDoesNotExist()
    {
        _mockDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(RedisValue.Null);

        var result = await _repository.GetByIdAsync(99);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsLock_When_BothKeysExist()
    {
        var entity = MakeLock();
        var lockKey = "lock:Order:1";

        _mockDb.SetupSequence(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(new RedisValue(lockKey))   // first call → lockid:1
               .ReturnsAsync(ToRedisJson(entity));       // second call → lock:Order:1

        var result = await _repository.GetByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
    }

    // ── AddAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_AssignsId_From_RedisCounter()
    {
        var entity = MakeLock();
        entity.Id = 0; // not yet assigned

        _mockDb.Setup(db => db.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(42L);
        _mockDb.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);
        _mockDb.Setup(db => db.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);
        _mockDb.Setup(db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);

        await _repository.AddAsync(entity);

        Assert.Equal(42, entity.Id);
    }

    [Fact]
    public async Task AddAsync_StoresLockKey_And_LockIdKey()
    {
        var entity = MakeLock();
        entity.Id = 0;

        _mockDb.Setup(db => db.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(1L);
        _mockDb.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);
        _mockDb.Setup(db => db.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);
        _mockDb.Setup(db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);

        await _repository.AddAsync(entity);

        // lock:Order:1 + lockid:1 = 2 StringSet calls
        _mockDb.Verify(db => db.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task AddAsync_AddsToAllTrackingSets_ConnectionUserAndType()
    {
        var entity = MakeLock(connectionId: "conn1");
        entity.Id = 0;

        _mockDb.Setup(db => db.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(1L);
        _mockDb.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);
        _mockDb.Setup(db => db.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);
        _mockDb.Setup(db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);

        await _repository.AddAsync(entity);

        // conn:conn1, user:user1:locks, type:Order:locks = 3 SetAdd calls
        _mockDb.Verify(db => db.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Exactly(3));
    }

    [Fact]
    public async Task AddAsync_SkipsConnectionSet_When_ConnectionIdIsNull()
    {
        var entity = MakeLock(connectionId: null!);
        entity.Id = 0;

        _mockDb.Setup(db => db.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(1L);
        _mockDb.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);
        _mockDb.Setup(db => db.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);
        _mockDb.Setup(db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);

        await _repository.AddAsync(entity);

        // user:user1:locks, type:Order:locks = 2 SetAdd calls (no connection)
        _mockDb.Verify(db => db.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Exactly(2));
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_RefreshesLockJson_And_TrackingKeyTtls()
    {
        var entity = MakeLock();

        _mockDb.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);
        _mockDb.Setup(db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);

        await _repository.UpdateAsync(entity);

        // Verify lock JSON is updated
        _mockDb.Verify(db => db.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            TimeSpan.FromSeconds(300), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.Once);
        
        // Verify TTL is refreshed on 3 tracking keys: lockid, user, entitytype
        _mockDb.Verify(db => db.KeyExpireAsync(
            It.IsAny<RedisKey>(), TimeSpan.FromSeconds(300), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()),
            Times.Exactly(3));
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_DeletesKeysAndRemovesFromSets()
    {
        var entity = MakeLock(connectionId: null!);

        _mockDb.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(2);
        _mockDb.Setup(db => db.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);

        await _repository.DeleteAsync(entity);

        // lock:Order:1 + lockid:1 = batch delete
        _mockDb.Verify(db => db.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Once);
        
        // user:user1:locks + type:Order:locks = 2 SetRemove calls (no connection)
        _mockDb.Verify(db => db.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DeleteAsync_RemovesFromAllSets_IncludingConnection()
    {
        var entity = MakeLock(connectionId: "conn1");

        _mockDb.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(2);
        _mockDb.Setup(db => db.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);

        await _repository.DeleteAsync(entity);

        // conn:conn1 + user:user1:locks + type:Order:locks = 3 SetRemove calls
        _mockDb.Verify(db => db.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Exactly(3));
    }

    // ── DeleteRangeAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteRangeAsync_CallsDeleteAsync_For_Each_Lock()
    {
        var locks = new List<EntityLock>
        {
            MakeLock(entityId: "1", connectionId: null!),
            MakeLock(entityId: "2", connectionId: null!)
        };

        _mockDb.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(2);
        _mockDb.Setup(db => db.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);

        await _repository.DeleteRangeAsync(locks);

        // 2 locks × 1 batch delete + 2 SetRemove each = 2 batch deletes + 4 SetRemove
        _mockDb.Verify(db => db.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Exactly(2));
        _mockDb.Verify(db => db.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Exactly(4));
    }

    // ── GetByConnectionIdAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetByConnectionIdAsync_ReturnsEmpty_When_NoSetMembers()
    {
        _mockDb.Setup(db => db.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(Array.Empty<RedisValue>());

        var result = await _repository.GetByConnectionIdAsync("conn1");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByConnectionIdAsync_ReturnsLocks_For_Each_SetMember()
    {
        var entity = MakeLock();

        _mockDb.Setup(db => db.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(new RedisValue[] { "lock:Order:1" });
        _mockDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(new RedisValue[] { ToRedisJson(entity) });

        var result = await _repository.GetByConnectionIdAsync("conn1");

        Assert.Single(result);
        Assert.Equal("Order", result[0].EntityType);
    }

    [Fact]
    public async Task GetByConnectionIdAsync_SkipsNullEntries_When_KeyExpiredBetweenCalls()
    {
        _mockDb.Setup(db => db.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(new RedisValue[] { "lock:Order:1" });
        _mockDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(RedisValue.Null);

        var result = await _repository.GetByConnectionIdAsync("conn1");

        Assert.Empty(result);
    }

    // ── GetByUserIdAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetByUserIdAsync_ReturnsLocks_For_User()
    {
        var entity = MakeLock();

        _mockDb.Setup(db => db.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(new RedisValue[] { "lock:Order:1" });
        _mockDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(new RedisValue[] { ToRedisJson(entity) });

        var result = await _repository.GetByUserIdAsync("user1");

        Assert.Single(result);
        Assert.Equal("user1", result[0].UserId);
    }
}