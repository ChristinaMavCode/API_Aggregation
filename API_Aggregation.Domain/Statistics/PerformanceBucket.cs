namespace API_Aggregation.Domain.Statistics;

/// <summary>
/// Performance classification of a single external API request.
/// </summary>
public enum PerformanceBucket
{
    Fast,
    Average,
    Slow
}
