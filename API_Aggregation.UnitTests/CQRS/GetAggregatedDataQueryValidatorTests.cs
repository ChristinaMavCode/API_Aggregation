using API_Aggregation.Application.CQRS.Queries.GetAggregatedData;

namespace API_Aggregation.UnitTests.CQRS;

public class GetAggregatedDataQueryValidatorTests
{
    private readonly GetAggregatedDataQueryValidator _validator = new();

    [Fact]
    public void Validate_Succeeds_ForAFullyPopulatedValidQuery()
    {
        var query = new GetAggregatedDataQuery(
            "Athens", "dotnet", "Weather", "OpenMeteo", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1),
            "rain", "Relevance", "asc", 2, 50);

        Assert.True(_validator.Validate(query).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Fails_WhenCityIsMissing(string? city)
    {
        var result = _validator.Validate(new GetAggregatedDataQuery(city));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetAggregatedDataQuery.City));
    }

    [Theory]
    [InlineData("music")]
    [InlineData("123")]
    public void Validate_Fails_ForAnUnknownCategory(string category) =>
        Assert.False(_validator.Validate(new GetAggregatedDataQuery("Athens", Category: category)).IsValid);

    [Theory]
    [InlineData("weather")]
    [InlineData("NEWS")]
    [InlineData("Repository")]
    public void Validate_AcceptsCategories_CaseInsensitive(string category) =>
        Assert.True(_validator.Validate(new GetAggregatedDataQuery("Athens", Category: category)).IsValid);

    [Fact]
    public void Validate_Fails_ForAnUnknownSortField() =>
        Assert.False(_validator.Validate(new GetAggregatedDataQuery("Athens", SortBy: "popularity")).IsValid);

    [Fact]
    public void Validate_Fails_ForAnInvalidSortOrder() =>
        Assert.False(_validator.Validate(new GetAggregatedDataQuery("Athens", SortOrder: "up")).IsValid);

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Validate_Fails_ForInvalidPaging(int page, int pageSize) =>
        Assert.False(_validator.Validate(new GetAggregatedDataQuery("Athens", Page: page, PageSize: pageSize)).IsValid);

    [Fact]
    public void Validate_Fails_WhenFromIsAfterTo()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.False(_validator.Validate(new GetAggregatedDataQuery("Athens", From: now, To: now.AddDays(-1))).IsValid);
    }
}
