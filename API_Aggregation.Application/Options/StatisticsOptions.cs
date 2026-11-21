namespace API_Aggregation.Application.Options;

public sealed class StatisticsOptions
{
    public const string SectionName = "Statistics";

    /// <summary>Requests faster than this value (ms) are classified as <c>Fast</c>.</summary>
    public double FastThresholdMs { get; set; } = 100;

    /// <summary>Requests slower than this value (ms) are <c>Slow</c>; everything in between is <c>Average</c>.</summary>
    public double SlowThresholdMs { get; set; } = 200;

    /// <summary>How long individual samples are retained (used for the recent-window averages).</summary>
    public int SampleRetentionMinutes { get; set; } = 60;

    /// <summary>Upper bound of retained samples per API, protecting memory under heavy load.</summary>
    public int MaxSamplesPerApi { get; set; } = 10_000;
}
