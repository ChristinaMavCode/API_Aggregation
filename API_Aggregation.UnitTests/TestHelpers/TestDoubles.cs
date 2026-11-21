using System.Net;
using System.Text;
using API_Aggregation.Application.Abstractions;
using API_Aggregation.Domain.Aggregation;
using Microsoft.Extensions.Options;

namespace API_Aggregation.UnitTests.TestHelpers;

/// <summary>Simple <see cref="IOptionsMonitor{TOptions}"/> supporting named options.</summary>
public sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class
{
    private readonly T _defaultValue;
    private readonly Dictionary<string, T> _named = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

    public TestOptionsMonitor(T defaultValue)
    {
        _defaultValue = defaultValue;
    }

    public TestOptionsMonitor<T> With(string name, T value)
    {
        _named[name] = value;
        return this;
    }

    public T CurrentValue => _defaultValue;

    public T Get(string? name) => name is not null && _named.TryGetValue(name, out var value) ? value : _defaultValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

/// <summary>Stub HTTP handler that answers with a callback and records the requests.</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public List<Uri> Requests { get; } = [];

    public static StubHttpMessageHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new StubHttpMessageHandler(_ => JsonResponse(json, status));

    public static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(_responder(request));
    }
}

/// <summary>Configurable fake external API client.</summary>
public sealed class FakeApiClient : IExternalApiClient
{
    private int _callCount;

    public FakeApiClient(string name, ItemCategory category = ItemCategory.News)
    {
        Name = name;
        Category = category;
    }

    public string Name { get; }

    public ItemCategory Category { get; }

    public Func<ExternalApiRequest, CancellationToken, Task<IReadOnlyList<AggregatedItem>>> Handler { get; set; } =
        (_, _) => Task.FromResult<IReadOnlyList<AggregatedItem>>([]);

    public int CallCount => _callCount;

    public List<ExternalApiRequest> ReceivedRequests { get; } = [];

    public Task<IReadOnlyList<AggregatedItem>> FetchAsync(ExternalApiRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        lock (ReceivedRequests)
            ReceivedRequests.Add(request);
        return Handler(request, cancellationToken);
    }

    public FakeApiClient Returning(params AggregatedItem[] items)
    {
        Handler = (_, _) => Task.FromResult<IReadOnlyList<AggregatedItem>>(items);
        return this;
    }

    public FakeApiClient Throwing(Exception exception)
    {
        Handler = (_, _) => Task.FromException<IReadOnlyList<AggregatedItem>>(exception);
        return this;
    }
}

public static class Items
{
    public static AggregatedItem Create(
        string title = "title",
        string source = "Source",
        ItemCategory category = ItemCategory.News,
        DateTimeOffset? timestamp = null,
        double relevance = 0.5,
        string? description = null) => new AggregatedItem
    {
        Source = source,
        Category = category,
        Title = title,
        Description = description,
        Timestamp = timestamp ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        Relevance = relevance
    };
}
