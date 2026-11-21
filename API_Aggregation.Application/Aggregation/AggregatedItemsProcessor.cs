using API_Aggregation.Domain.Aggregation;

namespace API_Aggregation.Application.Aggregation;

public enum AggregationSortBy
{
    Date,
    Relevance,
    Title,
    Source
}

/// <summary>Filtering, sorting and paging criteria applied to the aggregated items.</summary>
public sealed record AggregationCriteria
{
    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    /// <summary>Case-insensitive text that must appear in the title or description.</summary>
    public string? Search { get; init; }

    public AggregationSortBy SortBy { get; init; } = AggregationSortBy.Date;

    public bool Descending { get; init; } = true;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

public sealed record AggregationPage(IReadOnlyList<AggregatedItem> Items, int TotalItems);

/// <summary>
/// Pure, side-effect free filtering / sorting / paging of aggregated items.
/// (Category and source filters are applied earlier, when selecting which APIs to call,
/// so that we don't call APIs whose data would be discarded anyway.)
/// </summary>
public static class AggregatedItemsProcessor
{
    public static AggregationPage Apply(IEnumerable<AggregatedItem> items, AggregationCriteria criteria)
    {
        var query = items;

        if (criteria.From is DateTimeOffset from)
            query = query.Where(i => i.Timestamp >= from);

        if (criteria.To is DateTimeOffset to)
            query = query.Where(i => i.Timestamp <= to);

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var search = criteria.Search.Trim();
            query = query.Where(i =>
                i.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (i.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var filtered = _sort(query, criteria.SortBy, criteria.Descending).ToList();

        var page = filtered
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToList();

        return new AggregationPage(page, filtered.Count);
    }

    private static IOrderedEnumerable<AggregatedItem> _sort(IEnumerable<AggregatedItem> items, AggregationSortBy sortBy, bool descending)
    {
        // A secondary key (timestamp) keeps the ordering deterministic for equal primary keys.
        return sortBy switch
        {
            AggregationSortBy.Relevance => _order(items, i => i.Relevance, descending).ThenByDescending(i => i.Timestamp),
            AggregationSortBy.Title => _order(items, i => i.Title, descending, StringComparer.OrdinalIgnoreCase).ThenByDescending(i => i.Timestamp),
            AggregationSortBy.Source => _order(items, i => i.Source, descending, StringComparer.OrdinalIgnoreCase).ThenByDescending(i => i.Timestamp),
            _ => _order(items, i => i.Timestamp, descending).ThenBy(i => i.Source, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static IOrderedEnumerable<AggregatedItem> _order<TKey>(IEnumerable<AggregatedItem> items, Func<AggregatedItem, TKey> key, bool descending, IComparer<TKey>? comparer = null) =>
        descending ? items.OrderByDescending(key, comparer) : items.OrderBy(key, comparer);
}
