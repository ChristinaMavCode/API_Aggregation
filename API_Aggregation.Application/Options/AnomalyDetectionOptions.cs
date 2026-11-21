namespace API_Aggregation.Application.Options;

public sealed class AnomalyDetectionOptions
{
    public const string SectionName = "AnomalyDetection";

    public bool Enabled { get; set; } = true;

    /// <summary>How often the background service analyses the statistics.</summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>The recent window that is compared against the overall average.</summary>
    public int WindowMinutes { get; set; } = 5;

    /// <summary>An anomaly is reported when the recent average exceeds the overall average by more than this percentage.</summary>
    public double ThresholdPercent { get; set; } = 50;

    /// <summary>Minimum number of requests in the recent window for it to be considered meaningful.</summary>
    public int MinimumSamples { get; set; } = 3;
}
