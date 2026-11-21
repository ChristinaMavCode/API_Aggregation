using API_Aggregation.Application.Options;
using API_Aggregation.Domain.Statistics;
using API_Aggregation.Infrastructure.Statistics;
using API_Aggregation.UnitTests.TestHelpers;
using Microsoft.Extensions.Time.Testing;

namespace API_Aggregation.UnitTests.Statistics;

public class InMemoryApiStatisticsStoreTests
{
    private readonly FakeTimeProvider _time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly StatisticsOptions _options = new StatisticsOptions { FastThresholdMs = 100, SlowThresholdMs = 200, SampleRetentionMinutes = 60, MaxSamplesPerApi = 1000 };
    private readonly InMemoryApiStatisticsStore _store;

    public InMemoryApiStatisticsStoreTests()
    {
        _store = new InMemoryApiStatisticsStore(new TestOptionsMonitor<StatisticsOptions>(_options), _time);
    }

    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void GetSnapshots_ReturnsEmpty_WhenNothingWasRecorded() => Assert.Empty(_store.GetSnapshots());

    [Fact]
    public void Record_ComputesTotalsAveragesAndFailures()
    {
        _store.Record("Api", Ms(50), success: true);
        _store.Record("Api", Ms(150), success: true);
        _store.Record("Api", Ms(400), success: false);

        var snapshot = Assert.Single(_store.GetSnapshots());

        Assert.Equal("Api", snapshot.ApiName);
        Assert.Equal(3, snapshot.TotalRequests);
        Assert.Equal(1, snapshot.FailedRequests);
        Assert.Equal(200, snapshot.AverageResponseTimeMs);
    }

    [Fact]
    public void Record_GroupsRequestsIntoPerformanceBuckets()
    {
        _store.Record("Api", Ms(20), true);
        _store.Record("Api", Ms(80), true);
        _store.Record("Api", Ms(100), true);  // boundary -> Average
        _store.Record("Api", Ms(200), true);  // boundary -> Average
        _store.Record("Api", Ms(500), true);

        var buckets = Assert.Single(_store.GetSnapshots()).Buckets.ToDictionary(b => b.Bucket);

        Assert.Equal(2, buckets[PerformanceBucket.Fast].RequestCount);
        Assert.Equal(50, buckets[PerformanceBucket.Fast].AverageResponseTimeMs);
        Assert.Equal(2, buckets[PerformanceBucket.Average].RequestCount);
        Assert.Equal(150, buckets[PerformanceBucket.Average].AverageResponseTimeMs);
        Assert.Equal(1, buckets[PerformanceBucket.Slow].RequestCount);
        Assert.Equal(500, buckets[PerformanceBucket.Slow].AverageResponseTimeMs);

        Assert.Null(buckets[PerformanceBucket.Fast].LowerBoundMs);
        Assert.Equal(100, buckets[PerformanceBucket.Fast].UpperBoundMs);
        Assert.Null(buckets[PerformanceBucket.Slow].UpperBoundMs);
    }

    [Fact]
    public void Record_KeepsApisSeparate_AndTreatsNamesCaseInsensitively()
    {
        _store.Record("A", Ms(10), true);
        _store.Record("a", Ms(30), true);
        _store.Record("B", Ms(300), true);

        var snapshots = _store.GetSnapshots();

        Assert.Equal(2, snapshots.Count);
        Assert.Equal(2, snapshots.Single(s => s.ApiName == "A").TotalRequests);
        Assert.Equal(1, snapshots.Single(s => s.ApiName == "B").TotalRequests);
    }

    [Fact]
    public async Task Record_IsThreadSafe_UnderHeavyConcurrency()
    {
        const int threads = 16;
        const int perThread = 5_000;
        var apis = new[] { "A", "B", "C" };

        var tasks = Enumerable.Range(0, threads).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < perThread; i++)
            {
                _store.Record(apis[i % apis.Length], Ms(i % 2 == 0 ? 50 : 250), success: i % 10 != 0);
                if (i % 500 == 0)
                    _ = _store.GetSnapshots(); // concurrent readers
            }
        }));
        await Task.WhenAll(tasks);

        var snapshots = _store.GetSnapshots();
        Assert.Equal(threads * perThread, snapshots.Sum(s => s.TotalRequests));
        Assert.Equal(threads * perThread / 10, snapshots.Sum(s => s.FailedRequests));
        Assert.All(snapshots, s => Assert.Equal(s.TotalRequests, s.Buckets.Sum(b => b.RequestCount)));
        Assert.Equal(threads * perThread / 2, snapshots.Sum(s => s.Buckets.Single(b => b.Bucket == PerformanceBucket.Fast).RequestCount));
    }

    [Fact]
    public void GetPerformanceSummaries_SeparatesRecentWindowFromOverallAverage()
    {
        _store.Record("Api", Ms(100), true);
        _store.Record("Api", Ms(100), true);
        _time.Advance(TimeSpan.FromMinutes(10));
        _store.Record("Api", Ms(400), true);

        var summary = Assert.Single(_store.GetPerformanceSummaries(TimeSpan.FromMinutes(5)));

        Assert.Equal(3, summary.OverallRequestCount);
        Assert.Equal(200, summary.OverallAverageMs);
        Assert.Equal(1, summary.RecentRequestCount);
        Assert.Equal(400, summary.RecentAverageMs);
    }

    [Fact]
    public void Samples_ArePrunedAfterTheRetentionPeriod_ButTotalsAreKept()
    {
        _store.Record("Api", Ms(100), true);
        _time.Advance(TimeSpan.FromMinutes(61));

        var summary = Assert.Single(_store.GetPerformanceSummaries(TimeSpan.FromMinutes(120)));

        Assert.Equal(0, summary.RecentRequestCount);
        Assert.Equal(1, summary.OverallRequestCount);
    }

    [Fact]
    public void Samples_AreCappedPerApi()
    {
        _options.MaxSamplesPerApi = 5;
        for (var i = 0; i < 20; i++)
            _store.Record("Api", Ms(10), true);

        var summary = Assert.Single(_store.GetPerformanceSummaries(TimeSpan.FromMinutes(5)));

        Assert.Equal(5, summary.RecentRequestCount);
        Assert.Equal(20, summary.OverallRequestCount);
    }
}
