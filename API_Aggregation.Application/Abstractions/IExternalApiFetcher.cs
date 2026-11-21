using API_Aggregation.Domain.Aggregation;

namespace API_Aggregation.Application.Abstractions;

/// <summary>
/// Executes an <see cref="IExternalApiClient"/> applying caching, request de-duplication,
/// statistics recording and the fallback strategy. It never throws because of an API failure.
/// </summary>
public interface IExternalApiFetcher
{
    Task<SourceResult> FetchAsync(IExternalApiClient client, ExternalApiRequest request, CancellationToken cancellationToken);
}
