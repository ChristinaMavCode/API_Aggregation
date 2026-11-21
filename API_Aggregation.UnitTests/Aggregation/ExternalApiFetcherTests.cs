using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Aggregation;
using API_Aggregation.Application.Options;
using API_Aggregation.Domain.Aggregation;
using API_Aggregation.UnitTests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace API_Aggregation.UnitTests.Aggregation;

public class ExternalApiFetcherTests
{
    private static readonly ExternalApiRequest Request = new ExternalApiRequest("Athens", "dotnet");

    private readonly TestSystemClock _cacheClock = new TestSystemClock();
    private readonly MemoryCache _cache;
    private readonly Mock<IApiStatisticsStore> _statistics = new Mock<IApiStatisticsStore>();
    private readonly ExternalApiOptions _options = new ExternalApiOptions { CacheDurationSeconds = 60, StaleCacheDurationMinutes = 30 };
    private readonly ExternalApiFetcher _fetcher;

    public ExternalApiFetcherTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions { Clock = _cacheClock });
        _fetcher = new ExternalApiFetcher(
            _cache,
            _statistics.Object,
            new TestOptionsMonitor<ExternalApiOptions>(_options),
            new FakeTimeProvider(),
            NullLogger<ExternalApiFetcher>.Instance);
    }

    [Fact]
    public async Task FetchAsync_ReturnsLiveResult_AndRecordsSuccess_OnFirstCall()
    {
        var client = new FakeApiClient("Api").Returning(Items.Create("a"), Items.Create("b"));

        var result = await _fetcher.FetchAsync(client, Request, CancellationToken.None);

        Assert.Equal(SourceStatus.Live, result.Status);
        Assert.Equal(2, result.Items.Count);
        Assert.Null(result.Error);
        _statistics.Verify(s => s.Record("Api", It.IsAny<TimeSpan>(), true), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_ServesFromCache_WithoutCallingTheApiAgain()
    {
        var client = new FakeApiClient("Api").Returning(Items.Create("a"));

        await _fetcher.FetchAsync(client, Request, CancellationToken.None);
        var second = await _fetcher.FetchAsync(client, Request, CancellationToken.None);

        Assert.Equal(SourceStatus.Cached, second.Status);
        Assert.Single(second.Items);
        Assert.Equal(1, client.CallCount);
        // Cache hits are not external requests, so they must not be counted in the statistics.
        _statistics.Verify(s => s.Record(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_CallsTheApiAgain_AfterTheCacheExpires()
    {
        var client = new FakeApiClient("Api").Returning(Items.Create("a"));

        await _fetcher.FetchAsync(client, Request, CancellationToken.None);
        _cacheClock.Advance(TimeSpan.FromSeconds(61));
        var result = await _fetcher.FetchAsync(client, Request, CancellationToken.None);

        Assert.Equal(SourceStatus.Live, result.Status);
        Assert.Equal(2, client.CallCount);
    }

    [Fact]
    public async Task FetchAsync_ReturnsStaleData_WhenTheApiFailsAfterAPreviousSuccess()
    {
        var client = new FakeApiClient("Api").Returning(Items.Create("last known"));
        await _fetcher.FetchAsync(client, Request, CancellationToken.None);

        _cacheClock.Advance(TimeSpan.FromSeconds(61)); // fresh entry expired, stale one still alive
        client.Throwing(new HttpRequestException("boom"));

        var result = await _fetcher.FetchAsync(client, Request, CancellationToken.None);

        Assert.Equal(SourceStatus.Stale, result.Status);
        Assert.Equal("last known", Assert.Single(result.Items).Title);
        Assert.NotNull(result.Error);
        _statistics.Verify(s => s.Record("Api", It.IsAny<TimeSpan>(), false), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_ReturnsUnavailable_WhenTheApiFailsAndNoFallbackExists()
    {
        var client = new FakeApiClient("Api").Throwing(new HttpRequestException("secret internal details"));

        var result = await _fetcher.FetchAsync(client, Request, CancellationToken.None);

        Assert.Equal(SourceStatus.Unavailable, result.Status);
        Assert.Empty(result.Items);
        Assert.NotNull(result.Error);
        Assert.DoesNotContain("secret", result.Error); // exception details are logged, not leaked
        _statistics.Verify(s => s.Record("Api", It.IsAny<TimeSpan>(), false), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_ReturnsUnavailable_WhenTheStaleCacheHasAlsoExpired()
    {
        var client = new FakeApiClient("Api").Returning(Items.Create("old"));
        await _fetcher.FetchAsync(client, Request, CancellationToken.None);

        _cacheClock.Advance(TimeSpan.FromMinutes(31));
        client.Throwing(new TimeoutException());

        var result = await _fetcher.FetchAsync(client, Request, CancellationToken.None);

        Assert.Equal(SourceStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task FetchAsync_ReturnsDisabled_WithoutCallingTheApi_WhenDisabledByConfiguration()
    {
        _options.Enabled = false;
        var client = new FakeApiClient("Api").Returning(Items.Create());

        var result = await _fetcher.FetchAsync(client, Request, CancellationToken.None);

        Assert.Equal(SourceStatus.Disabled, result.Status);
        Assert.Equal(0, client.CallCount);
    }


    [Fact]
    public async Task FetchAsync_DeduplicatesConcurrentIdenticalRequests()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<AggregatedItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeApiClient("Api") { Handler = (_, _) => gate.Task };

        var calls = Enumerable.Range(0, 10)
            .Select(_ => _fetcher.FetchAsync(client, Request, CancellationToken.None))
            .ToList();

        gate.SetResult([Items.Create("shared")]);
        var results = await Task.WhenAll(calls);

        Assert.Equal(1, client.CallCount);
        Assert.All(results, r => Assert.Equal("shared", Assert.Single(r.Items).Title));
    }

    [Fact]
    public async Task FetchAsync_UsesSeparateCacheEntries_ForDifferentRequests()
    {
        var client = new FakeApiClient("Api").Returning(Items.Create());

        await _fetcher.FetchAsync(client, new ExternalApiRequest("Athens", "x"), CancellationToken.None);
        await _fetcher.FetchAsync(client, new ExternalApiRequest("London", "x"), CancellationToken.None);

        Assert.Equal(2, client.CallCount);
    }

    [Fact]
    public async Task FetchAsync_PropagatesCancellation_RequestedByTheCaller()
    {
        var never = new TaskCompletionSource<IReadOnlyList<AggregatedItem>>();
        var client = new FakeApiClient("Api") { Handler = (_, _) => never.Task };
        using var cts = new CancellationTokenSource();

        var task = _fetcher.FetchAsync(client, Request, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    private sealed class TestSystemClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => UtcNow += by;
    }
}
