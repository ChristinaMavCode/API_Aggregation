using API_Aggregation.Application.Options;
using API_Aggregation.Application.Statistics;
using API_Aggregation.Domain.Statistics;
using API_Aggregation.Infrastructure.BackgroundServices;
using API_Aggregation.Infrastructure.Statistics;
using API_Aggregation.UnitTests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace API_Aggregation.UnitTests.Statistics;

public class PerformanceAnomalyTests
{
    private readonly FakeTimeProvider _time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly AnomalyDetectionOptions _anomalyOptions = new AnomalyDetectionOptions { WindowMinutes = 5, ThresholdPercent = 50, MinimumSamples = 3 };
    private readonly InMemoryApiStatisticsStore _store;
    private readonly PerformanceAnomalyDetector _detector;

    public PerformanceAnomalyTests()
    {
        _store = new InMemoryApiStatisticsStore(new TestOptionsMonitor<StatisticsOptions>(new StatisticsOptions()), _time);
        _detector = new PerformanceAnomalyDetector(_store, new TestOptionsMonitor<AnomalyDetectionOptions>(_anomalyOptions));
    }

    private void Record(string api, int count, double ms)
    {
        for (var i = 0; i < count; i++)
            _store.Record(api, TimeSpan.FromMilliseconds(ms), true);
    }

    [Fact]
    public void Detect_ReportsAnApi_WhoseRecentAverageIsMoreThan50PercentAboveItsOverallAverage()
    {
        Record("Slow", 10, 100);          // historical baseline
        _time.Advance(TimeSpan.FromMinutes(10));
        Record("Slow", 3, 400);           // recent spike: overall = 2200/13 ≈ 169ms, recent = 400ms

        var anomaly = Assert.Single(_detector.Detect());

        Assert.Equal("Slow", anomaly.ApiName);
        Assert.Equal(400, anomaly.RecentAverageMs);
        Assert.Equal(169.23, anomaly.OverallAverageMs);
        Assert.True(anomaly.DeviationPercent > 50);
        Assert.Equal(3, anomaly.RecentRequestCount);
    }

    [Fact]
    public void Detect_ReportsNothing_WhenPerformanceIsStable()
    {
        Record("Stable", 10, 100);
        _time.Advance(TimeSpan.FromMinutes(10));
        Record("Stable", 5, 120);

        Assert.Empty(_detector.Detect());
    }

    [Fact]
    public void Detect_IgnoresTheRecentWindow_WhenItHasTooFewSamples()
    {
        Record("Api", 10, 100);
        _time.Advance(TimeSpan.FromMinutes(10));
        Record("Api", 2, 1000);

        Assert.Empty(_detector.Detect());
    }

    [Fact]
    public void Detect_OnlyReportsTheDegradedApi()
    {
        Record("Good", 10, 100);
        Record("Bad", 10, 100);
        _time.Advance(TimeSpan.FromMinutes(10));
        Record("Good", 3, 100);
        Record("Bad", 3, 900);

        Assert.Equal("Bad", Assert.Single(_detector.Detect()).ApiName);
    }

    [Fact]
    public void BackgroundService_LogsAWarning_ForEveryAnomaly()
    {
        Record("Slow", 10, 100);
        _time.Advance(TimeSpan.FromMinutes(10));
        Record("Slow", 3, 400);
        var logger = new FakeLogger<PerformanceAnomalyBackgroundService>();
        var service = new PerformanceAnomalyBackgroundService(
            _detector, new TestOptionsMonitor<AnomalyDetectionOptions>(_anomalyOptions), _time, logger);

        service.Analyze();

        var record = Assert.Single(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Contains("Performance anomaly detected for Slow", record.Message);
    }

    [Theory]
    [InlineData(0, PerformanceBucket.Fast)]
    [InlineData(99.9, PerformanceBucket.Fast)]
    [InlineData(100, PerformanceBucket.Average)]
    [InlineData(200, PerformanceBucket.Average)]
    [InlineData(200.1, PerformanceBucket.Slow)]
    public void BucketClassifier_UsesTheConfiguredThresholds(double ms, PerformanceBucket expected) =>
        Assert.Equal(expected, PerformanceBucketClassifier.Classify(ms, new StatisticsOptions()));
}
