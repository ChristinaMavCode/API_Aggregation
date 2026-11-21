using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.CQRS.Queries.GetAggregatedData;
using API_Aggregation.Application.Mapping;
using API_Aggregation.Domain.Aggregation;
using API_Aggregation.UnitTests.TestHelpers;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace API_Aggregation.UnitTests.CQRS;

public class GetAggregatedDataQueryHandlerTests
{
    private readonly FakeApiClient _weather = new FakeApiClient("Weather1", ItemCategory.Weather);
    private readonly FakeApiClient _news = new FakeApiClient("News1", ItemCategory.News);
    private readonly FakeApiClient _repos = new FakeApiClient("Repos1", ItemCategory.Repository);
    private readonly Mock<IExternalApiFetcher> _fetcher = new Mock<IExternalApiFetcher>();

    public GetAggregatedDataQueryHandlerTests()
    {
        // By default the fetcher simply delegates to the client and reports a live result.
        _fetcher
            .Setup(f => f.FetchAsync(It.IsAny<IExternalApiClient>(), It.IsAny<ExternalApiRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (IExternalApiClient c, ExternalApiRequest r, CancellationToken ct) =>
                new SourceResult(c.Name, SourceStatus.Live, await c.FetchAsync(r, ct), 10));
    }

    private GetAggregatedDataQueryHandler CreateHandler(IExternalApiFetcher? fetcher = null) =>
        new GetAggregatedDataQueryHandler([_weather, _news, _repos], fetcher ?? _fetcher.Object, new AggregationMapper(), new FakeTimeProvider());

    [Fact]
    public async Task Handle_AggregatesItemsFromAllApis()
    {
        _weather.Returning(Items.Create("w", "Weather1", ItemCategory.Weather));
        _news.Returning(Items.Create("n", "News1"));
        _repos.Returning(Items.Create("r", "Repos1", ItemCategory.Repository));

        var result = await CreateHandler().Handle(new GetAggregatedDataQuery("Athens"), CancellationToken.None);

        Assert.Equal(3, result.TotalItems);
        Assert.Equal(3, result.Sources.Count);
        Assert.Equivalent(new[] { "w", "n", "r" }, result.Items.Select(i => i.Title));
        Assert.All(result.Sources, s => Assert.Equal("Live", s.Status));
    }

    [Fact]
    public async Task Handle_CallsTheApisInParallel()
    {
        // Each client only completes once ALL clients have been started. If the handler awaited the
        // APIs sequentially, the first client would never complete and the test would time out.
        var started = 0;
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        foreach (var client in new[] { _weather, _news, _repos })
        {
            client.Handler = async (_, _) =>
            {
                if (Interlocked.Increment(ref started) == 3)
                    allStarted.SetResult();
                await allStarted.Task;
                return [];
            };
        }

        var task = CreateHandler().Handle(new GetAggregatedDataQuery("Athens"), CancellationToken.None);

        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Same(task, completed);
    }

    [Fact]
    public async Task Handle_OnlyCallsApisOfTheRequestedCategory()
    {
        await CreateHandler().Handle(new GetAggregatedDataQuery("Athens", Category: "weather"), CancellationToken.None);

        Assert.Equal(1, _weather.CallCount);
        Assert.Equal(0, _news.CallCount);
        Assert.Equal(0, _repos.CallCount);
    }

    [Fact]
    public async Task Handle_OnlyCallsTheRequestedSources()
    {
        var result = await CreateHandler().Handle(new GetAggregatedDataQuery("Athens", Sources: "news1, REPOS1"), CancellationToken.None);

        Assert.Equal(0, _weather.CallCount);
        Assert.Equal(1, _news.CallCount);
        Assert.Equal(1, _repos.CallCount);
        Assert.Equal(["News1", "Repos1"], result.Sources.Select(s => s.Source));
    }

    [Fact]
    public async Task Handle_UsesTheCityAsTopic_WhenNoTopicIsGiven()
    {
        await CreateHandler().Handle(new GetAggregatedDataQuery("  Athens "), CancellationToken.None);

        Assert.Equal(new ExternalApiRequest("Athens", "Athens"), Assert.Single(_news.ReceivedRequests));
    }

    [Fact]
    public async Task Handle_PassesTheTopic_WhenGiven()
    {
        await CreateHandler().Handle(new GetAggregatedDataQuery("Athens", Topic: "dotnet"), CancellationToken.None);

        Assert.Equal("dotnet", Assert.Single(_news.ReceivedRequests).Topic);
    }

    [Fact]
    public async Task Handle_ReturnsPartialData_WhenOneApiIsUnavailable()
    {
        _news.Returning(Items.Create("n", "News1"));
        _fetcher
            .Setup(f => f.FetchAsync(_weather, It.IsAny<ExternalApiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SourceResult("Weather1", SourceStatus.Unavailable, [], 5, "down"));

        var result = await CreateHandler().Handle(new GetAggregatedDataQuery("Athens"), CancellationToken.None);

        Assert.Equal("n", Assert.Single(result.Items).Title);
        var weather = Assert.Single(result.Sources, s => s.Source == "Weather1");
        Assert.Equal("Unavailable", weather.Status);
        Assert.Equal("down", weather.Error);
    }

    [Fact]
    public async Task Handle_AppliesSortingAndPaging()
    {
        _news.Returning(
            Items.Create("low", "News1", relevance: 0.1),
            Items.Create("high", "News1", relevance: 0.9),
            Items.Create("mid", "News1", relevance: 0.5));

        var result = await CreateHandler().Handle(
            new GetAggregatedDataQuery("Athens", SortBy: "relevance", SortOrder: "desc", Page: 1, PageSize: 2),
            CancellationToken.None);

        Assert.Equal(["high", "mid"], result.Items.Select(i => i.Title));
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.PageSize);
    }
}
