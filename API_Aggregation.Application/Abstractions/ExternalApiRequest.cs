namespace API_Aggregation.Application.Abstractions;

/// <summary>
/// Parameters passed to every external API client.
/// </summary>
/// <param name="City">Location used by the weather APIs.</param>
/// <param name="Topic">Search term used by the news / repository APIs.</param>
public sealed record ExternalApiRequest(string City, string Topic);
