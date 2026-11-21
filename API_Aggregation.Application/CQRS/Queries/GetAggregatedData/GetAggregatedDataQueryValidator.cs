using API_Aggregation.Application.Aggregation;
using API_Aggregation.Domain.Aggregation;
using FluentValidation;

namespace API_Aggregation.Application.CQRS.Queries.GetAggregatedData;

public sealed class GetAggregatedDataQueryValidator : AbstractValidator<GetAggregatedDataQuery>
{
    private static readonly string[] SortOrders = ["asc", "desc"];

    public GetAggregatedDataQueryValidator()
    {
        RuleFor(q => q.City)
            .NotEmpty().WithMessage("'city' is required.")
            .MaximumLength(100);

        RuleFor(q => q.Topic).MaximumLength(100);
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.Sources).MaximumLength(500);

        RuleFor(q => q.Category)
            .Must(_beDefinedEnum<ItemCategory>)
            .When(q => !string.IsNullOrWhiteSpace(q.Category))
            .WithMessage($"'category' must be one of: {string.Join(", ", Enum.GetNames<ItemCategory>())}.");

        RuleFor(q => q.SortBy)
            .Must(_beDefinedEnum<AggregationSortBy>)
            .When(q => !string.IsNullOrWhiteSpace(q.SortBy))
            .WithMessage($"'sortBy' must be one of: {string.Join(", ", Enum.GetNames<AggregationSortBy>())}.");

        RuleFor(q => q.SortOrder)
            .Must(o => SortOrders.Contains(o!, StringComparer.OrdinalIgnoreCase))
            .When(q => !string.IsNullOrWhiteSpace(q.SortOrder))
            .WithMessage("'sortOrder' must be 'asc' or 'desc'.");

        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).When(q => q.Page.HasValue);

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, GetAggregatedDataQuery.MaxPageSize)
            .When(q => q.PageSize.HasValue);

        RuleFor(q => q)
            .Must(q => q.From <= q.To)
            .When(q => q.From.HasValue && q.To.HasValue)
            .WithMessage("'from' must be earlier than or equal to 'to'.");
    }

    private static bool _beDefinedEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed);
}
