using API_Aggregation.Domain.Statistics;

namespace API_Aggregation.Application.Abstractions;

/// <summary>
/// Thread-safe store for the request statistics of the external APIs.
/// </summary>
public interface IApiStatisticsStore
{
    /// <summary>Records one call to an external API.</summary>
    void Record(string apiName, TimeSpan duration, bool success);

    /// <summary>Returns a consistent snapshot of the statistics of every API that has been called.</summary>
    IReadOnlyList<ApiStatisticsSnapshot> GetSnapshots();

    /// <summary>Returns, for each API, the overall average together with the average of the given recent window.</summary>
    IReadOnlyList<ApiPerformanceSummary> GetPerformanceSummaries(TimeSpan recentWindow);
}
