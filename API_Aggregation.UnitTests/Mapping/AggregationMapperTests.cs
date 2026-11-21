using API_Aggregation.Application.Mapping;
using API_Aggregation.Domain.Aggregation;
using API_Aggregation.Domain.Statistics;
using API_Aggregation.UnitTests.TestHelpers;

namespace API_Aggregation.UnitTests.Mapping;

public class AggregationMapperTests
{
    private readonly AggregationMapper _mapper = new AggregationMapper();

    [Fact]
    public void ToDto_MapsAnAggregatedItem_IncludingEnumAsNameAndMetadata()
    {
        var item = new AggregatedItem
        {
            Source = "OpenMeteo",
            Category = ItemCategory.Weather,
            Title = "Athens",
            Description = "sunny",
            Timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Relevance = 0.7,
            Url = "https://example.com",
            Metadata = new Dictionary<string, string> { ["temperatureC"] = "20" }
        };

        var dto = _mapper.ToDto(item);

        Assert.Equal("OpenMeteo", dto.Source);
        Assert.Equal("Weather", dto.Category);
        Assert.Equal("Athens", dto.Title);
        Assert.Equal("sunny", dto.Description);
        Assert.Equal(item.Timestamp, dto.Timestamp);
        Assert.Equal(0.7, dto.Relevance);
        Assert.Equal("https://example.com", dto.Url);
        Assert.Equal("20", dto.Metadata["temperatureC"]);
    }

    [Fact]
    public void ToDto_MapsASourceResult_WithItemCountInsteadOfItems()
    {
        var result = new SourceResult("HackerNews", SourceStatus.Stale, [Items.Create(), Items.Create()], 123.4, "fallback");

        var dto = _mapper.ToDto(result);

        Assert.Equal("HackerNews", dto.Source);
        Assert.Equal("Stale", dto.Status);
        Assert.Equal(2, dto.ItemCount);
        Assert.Equal(123.4, dto.ResponseTimeMs);
        Assert.Equal("fallback", dto.Error);
    }

    [Fact]
    public void ToDto_MapsStatisticsSnapshotsWithBuckets()
    {
        var snapshot = new ApiStatisticsSnapshot("GitHub", 3, 1, 180, [new BucketStatistics(PerformanceBucket.Slow, 200, null, 1, 300)]);

        var dto = _mapper.ToDto(snapshot);

        Assert.Equal("GitHub", dto.ApiName);
        Assert.Equal(3, dto.TotalRequests);
        Assert.Equal(1, dto.FailedRequests);
        Assert.Equal(180, dto.AverageResponseTimeMs);
        var bucket = Assert.Single(dto.Buckets);
        Assert.Equal("Slow", bucket.Bucket);
        Assert.Equal(200, bucket.LowerBoundMs);
        Assert.Null(bucket.UpperBoundMs);
        Assert.Equal(300, bucket.AverageResponseTimeMs);
    }
}
