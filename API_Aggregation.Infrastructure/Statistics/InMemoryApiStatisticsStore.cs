using System.Collections.Concurrent;
using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Options;
using API_Aggregation.Application.Statistics;
using API_Aggregation.Domain.Statistics;
using Microsoft.Extensions.Options;

namespace API_Aggregation.Infrastructure.Statistics;

/// <summary>
/// In-memory, thread-safe statistics store.
/// <para>
/// Concurrency design: a <see cref="ConcurrentDictionary{TKey,TValue}"/> gives lock-free lookup/creation of
/// the per-API accumulator, and each accumulator is protected by its own lock. Locking per API (instead of a
/// global lock) means requests to different APIs never contend, while the lock guarantees that the counters,
/// totals and samples of one API are always updated and read together (a consistent snapshot) - something
/// independent <c>Interlocked</c> operations on several fields could not guarantee.
/// The critical sections are a handful of arithmetic operations, so contention is negligible.
/// </para>
/// </summary>
public sealed class InMemoryApiStatisticsStore : IApiStatisticsStore
{
    private static readonly PerformanceBucket[] AllBuckets = Enum.GetValues<PerformanceBucket>();

    private readonly ConcurrentDictionary<string, ApiAccumulator> _apis = new ConcurrentDictionary<string, ApiAccumulator>(StringComparer.OrdinalIgnoreCase);
    private readonly IOptionsMonitor<StatisticsOptions> _optionsMonitor;
    private readonly TimeProvider _timeProvider;

    public InMemoryApiStatisticsStore(IOptionsMonitor<StatisticsOptions> optionsMonitor, TimeProvider timeProvider)
    {
        _optionsMonitor = optionsMonitor;
        _timeProvider = timeProvider;
    }

    public void Record(string apiName, TimeSpan duration, bool success)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiName);

        var options = _optionsMonitor.CurrentValue;
        var elapsedMs = duration.TotalMilliseconds;
        var bucket = PerformanceBucketClassifier.Classify(elapsedMs, options);
        var now = _timeProvider.GetUtcNow();

        var accumulator = _apis.GetOrAdd(apiName, static name => new ApiAccumulator(name));

        lock (accumulator.Gate)
        {
            accumulator.TotalRequests++;
            accumulator.TotalMs += elapsedMs;
            if (!success)
                accumulator.FailedRequests++;

            accumulator.BucketCounts[(int)bucket]++;
            accumulator.BucketTotalMs[(int)bucket] += elapsedMs;

            accumulator.Samples.Enqueue(new Sample(now, elapsedMs));
            _prune(accumulator, now, options);
        }
    }

    public IReadOnlyList<ApiStatisticsSnapshot> GetSnapshots()
    {
        var options = _optionsMonitor.CurrentValue;
        var snapshots = new List<ApiStatisticsSnapshot>(_apis.Count);
        foreach (var accumulator in _apis.Values)
        {
            lock (accumulator.Gate)
            {
                var buckets = AllBuckets
                    .Select(b =>
                    {
                        var (lower, upper) = PerformanceBucketClassifier.GetBounds(b, options);
                        var count = accumulator.BucketCounts[(int)b];
                        return new BucketStatistics(b, lower, upper, count, _average(accumulator.BucketTotalMs[(int)b], count));
                    })
                    .ToList();

                snapshots.Add(new ApiStatisticsSnapshot(
                    accumulator.ApiName,
                    accumulator.TotalRequests,
                    accumulator.FailedRequests,
                    _average(accumulator.TotalMs, accumulator.TotalRequests),
                    buckets));
            }
        }

        return snapshots.OrderBy(s => s.ApiName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<ApiPerformanceSummary> GetPerformanceSummaries(TimeSpan recentWindow)
    {
        var options = _optionsMonitor.CurrentValue;
        var now = _timeProvider.GetUtcNow();
        var windowStart = now - recentWindow;
        var summaries = new List<ApiPerformanceSummary>(_apis.Count);

        foreach (var accumulator in _apis.Values)
        {
            lock (accumulator.Gate)
            {
                _prune(accumulator, now, options);

                long recentCount = 0;
                double recentTotal = 0;
                foreach (var sample in accumulator.Samples)
                {
                    if (sample.Timestamp < windowStart)
                        continue;
                    recentCount++;
                    recentTotal += sample.ElapsedMs;
                }

                summaries.Add(new ApiPerformanceSummary(
                    accumulator.ApiName,
                    accumulator.TotalRequests,
                    _average(accumulator.TotalMs, accumulator.TotalRequests),
                    recentCount,
                    _average(recentTotal, recentCount)));
            }
        }

        return summaries;
    }

    /// <summary>Drops samples older than the retention period or above the size cap. Caller must hold the lock.</summary>
    private static void _prune(ApiAccumulator accumulator, DateTimeOffset now, StatisticsOptions options)
    {
        var cutoff = now - TimeSpan.FromMinutes(options.SampleRetentionMinutes);

        while (accumulator.Samples.Count > 0 &&
               (accumulator.Samples.Count > options.MaxSamplesPerApi || accumulator.Samples.Peek().Timestamp < cutoff))
        {
            accumulator.Samples.Dequeue();
        }
    }

    private static double _average(double total, long count) => count == 0 ? 0 : Math.Round(total / count, 2);

    private readonly record struct Sample(DateTimeOffset Timestamp, double ElapsedMs);

    private sealed class ApiAccumulator
    {
        public ApiAccumulator(string apiName)
        {
            ApiName = apiName;
        }

        public object Gate { get; } = new object();
        public string ApiName { get; }
        public long TotalRequests;
        public long FailedRequests;
        public double TotalMs;
        public readonly long[] BucketCounts = new long[AllBuckets.Length];
        public readonly double[] BucketTotalMs = new double[AllBuckets.Length];
        public readonly Queue<Sample> Samples = new Queue<Sample>();
    }
}
