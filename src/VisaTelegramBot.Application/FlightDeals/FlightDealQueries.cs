using FluentValidation;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.FlightDeals;

namespace VisaTelegramBot.Application.FlightDeals;

public sealed record ListFlightRoutesQuery : IQuery<IReadOnlyList<FlightRouteResponse>>;

internal sealed class ListFlightRoutesQueryHandler(IFlightDealQueries queries)
    : IQueryHandler<ListFlightRoutesQuery, IReadOnlyList<FlightRouteResponse>>
{
    public async Task<Result<IReadOnlyList<FlightRouteResponse>>> Handle(ListFlightRoutesQuery query, CancellationToken cancellationToken) =>
        Result.Success(await queries.ListRoutesAsync(cancellationToken));
}

public sealed record ListFlightDealsQuery(FlightDealStatus? Status, int Limit = 50) : IQuery<IReadOnlyList<FlightDealResponse>>;

internal sealed class ListFlightDealsQueryValidator : AbstractValidator<ListFlightDealsQuery>
{
    public ListFlightDealsQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 200);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
    }
}

internal sealed class ListFlightDealsQueryHandler(IFlightDealQueries queries)
    : IQueryHandler<ListFlightDealsQuery, IReadOnlyList<FlightDealResponse>>
{
    public async Task<Result<IReadOnlyList<FlightDealResponse>>> Handle(ListFlightDealsQuery query, CancellationToken cancellationToken) =>
        Result.Success(await queries.ListDealsAsync(query.Status, query.Limit, cancellationToken));
}

public sealed record GetDueFlightRouteIdsQuery(int Limit) : IQuery<IReadOnlyList<Guid>>;

internal sealed class GetDueFlightRouteIdsQueryHandler(IFlightRouteRepository repository, TimeProvider timeProvider)
    : IQueryHandler<GetDueFlightRouteIdsQuery, IReadOnlyList<Guid>>
{
    public async Task<Result<IReadOnlyList<Guid>>> Handle(GetDueFlightRouteIdsQuery query, CancellationToken cancellationToken) =>
        Result.Success(await repository.GetDueIdsAsync(timeProvider.GetUtcNow().UtcDateTime, query.Limit, cancellationToken));
}
