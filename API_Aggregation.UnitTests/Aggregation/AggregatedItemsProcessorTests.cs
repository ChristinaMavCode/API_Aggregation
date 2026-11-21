using API_Aggregation.Application.Aggregation;
using API_Aggregation.UnitTests.TestHelpers;

namespace API_Aggregation.UnitTests.Aggregation;

public class AggregatedItemsProcessorTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<API_Aggregation.Domain.Aggregation.AggregatedItem> Data =
    [
        Items.Create("Bravo", "GitHub", timestamp: Day1, relevance: 0.2, description: "rain gear"),
        Items.Create("alpha", "HackerNews", timestamp: Day1.AddDays(1), relevance: 0.9),
        Items.Create("Charlie", "OpenMeteo", timestamp: Day1.AddDays(2), relevance: 0.5, description: "Light RAIN")
    ];

    [Fact]
    public void Apply_SortsByDateDescending_ByDefault()
    {
        var page = AggregatedItemsProcessor.Apply(Data, new AggregationCriteria());

        Assert.Equal(["Charlie", "alpha", "Bravo"], page.Items.Select(i => i.Title));
    }

    [Theory]
    [InlineData(AggregationSortBy.Date, false, new[] { "Bravo", "alpha", "Charlie" })]
    [InlineData(AggregationSortBy.Relevance, true, new[] { "alpha", "Charlie", "Bravo" })]
    [InlineData(AggregationSortBy.Relevance, false, new[] { "Bravo", "Charlie", "alpha" })]
    [InlineData(AggregationSortBy.Title, false, new[] { "alpha", "Bravo", "Charlie" })]
    [InlineData(AggregationSortBy.Source, true, new[] { "Charlie", "alpha", "Bravo" })]
    public void Apply_SortsByTheRequestedField(AggregationSortBy sortBy, bool descending, string[] expected)
    {
        var page = AggregatedItemsProcessor.Apply(Data, new AggregationCriteria { SortBy = sortBy, Descending = descending });

        Assert.Equal(expected, page.Items.Select(i => i.Title));
    }

    [Fact]
    public void Apply_FiltersByDateRange_Inclusive()
    {
        var page = AggregatedItemsProcessor.Apply(Data, new AggregationCriteria { From = Day1.AddDays(1), To = Day1.AddDays(2) });

        Assert.Equal(2, page.TotalItems);
        Assert.DoesNotContain(page.Items, i => i.Title == "Bravo");
    }

    [Fact]
    public void Apply_FiltersBySearchText_InTitleOrDescription_CaseInsensitive()
    {
        var page = AggregatedItemsProcessor.Apply(Data, new AggregationCriteria { Search = "rain" });

        Assert.Equal(["Charlie", "Bravo"], page.Items.Select(i => i.Title));
    }

    [Fact]
    public void Apply_PagesResults_AndReportsTheTotalBeforePaging()
    {
        var page = AggregatedItemsProcessor.Apply(Data, new AggregationCriteria { Page = 2, PageSize = 2 });

        Assert.Equal(3, page.TotalItems);
        Assert.Equal("Bravo", Assert.Single(page.Items).Title);
    }

    [Fact]
    public void Apply_ReturnsEmptyPage_WhenPageIsOutOfRange()
    {
        var page = AggregatedItemsProcessor.Apply(Data, new AggregationCriteria { Page = 5, PageSize = 2 });

        Assert.Empty(page.Items);
        Assert.Equal(3, page.TotalItems);
    }
}
