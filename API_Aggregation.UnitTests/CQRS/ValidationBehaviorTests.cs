using API_Aggregation.Application.CQRS.Behaviors;
using API_Aggregation.Application.CQRS.Queries.GetAggregatedData;
using API_Aggregation.Application.Dtos;
using FluentValidation;

namespace API_Aggregation.UnitTests.CQRS;

public class ValidationBehaviorTests
{
    private readonly ValidationBehavior<GetAggregatedDataQuery, AggregationResponseDto> _behavior =
        new([new GetAggregatedDataQueryValidator()]);

    [Fact]
    public async Task Handle_Throws_AndDoesNotCallTheHandler_WhenTheRequestIsInvalid()
    {
        var handlerCalled = false;

        await Assert.ThrowsAsync<ValidationException>(() => _behavior.Handle(
            new GetAggregatedDataQuery(City: null),
            () => { handlerCalled = true; return Task.FromResult<AggregationResponseDto>(null!); },
            CancellationToken.None));

        Assert.False(handlerCalled);
    }

    [Fact]
    public async Task Handle_CallsTheHandler_WhenTheRequestIsValid()
    {
        var handlerCalled = false;

        await _behavior.Handle(
            new GetAggregatedDataQuery("Athens"),
            () => { handlerCalled = true; return Task.FromResult<AggregationResponseDto>(null!); },
            CancellationToken.None);

        Assert.True(handlerCalled);
    }
}
