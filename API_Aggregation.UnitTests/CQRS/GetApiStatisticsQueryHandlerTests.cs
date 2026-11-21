using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.CQRS.Queries.GetApiStatistics;
using API_Aggregation.Application.Mapping;
using API_Aggregation.Application.Options;
using API_Aggregation.Domain.Statistics;
using API_Aggregation.UnitTests.TestHelpers;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace API_Aggregation.UnitTests.CQRS;

public class GetApiStatisticsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsStatistics_IncludingRegisteredApisThatWereNeverCalled()
    {
        var store = new Mock<IApiStatisticsStore>();
        store.Setup(s => s.GetSnapshots()).Returns(
        [
            new ApiStatisticsSnapshot("Called", 4, 1, 150, [new BucketStatistics(PerformanceBucket.Average, 100, 200, 4, 150)])
        ]);

        var options = new StatisticsOptions { FastThresholdMs = 50, SlowThresholdMs = 300 };
        var handler = new GetApiStatisticsQueryHandler(
            store.Object,
            [new FakeApiClient("Called"), new FakeApiClient("NeverCalled")],
            new AggregationMapper(),
            new TestOptionsMonitor<StatisticsOptions>(options),
            new FakeTimeProvider());

        var result = await handler.Handle(new GetApiStatisticsQuery(), CancellationToken.None);

        Assert.Equal(50, result.FastThresholdMs);
        Assert.Equal(300, result.SlowThresholdMs);
        Assert.Equal(["Called", "NeverCalled"], result.Apis.Select(a => a.ApiName));

        var called = result.Apis[0];
        Assert.Equal(4, called.TotalRequests);
        Assert.Equal(1, called.FailedRequests);
        Assert.Equal("Average", Assert.Single(called.Buckets).Bucket);

        var neverCalled = result.Apis[1];
        Assert.Equal(0, neverCalled.TotalRequests);
        Assert.Equal(["Fast", "Average", "Slow"], neverCalled.Buckets.Select(b => b.Bucket));
        Assert.Equal(50, neverCalled.Buckets[0].UpperBoundMs);
    }
}
