using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Application.FlightDeals;

/// <summary>Firsati Turkce kanal mesajina donusturup hemen gonderim sirasina alir.</summary>
public sealed record PublishFlightDealCommand(Guid FlightDealId) : ICommand<Guid>;

internal sealed class PublishFlightDealCommandHandler(
    IFlightDealRepository dealRepository,
    IFlightRouteRepository routeRepository,
    IChannelMessageRepository messageRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<PublishFlightDealCommand, Guid>
{
    public async Task<Result<Guid>> Handle(PublishFlightDealCommand command, CancellationToken cancellationToken)
    {
        var deal = await dealRepository.GetByIdAsync(command.FlightDealId, cancellationToken);

        if (deal is null)
        {
            return FlightDealErrors.DealNotFound(command.FlightDealId);
        }

        if (deal.ChannelMessageId is { } existingMessageId)
        {
            // Idempotent: iki kez tiklanirsa ikinci mesaj olusmaz.
            return existingMessageId;
        }

        var route = await routeRepository.GetByIdAsync(deal.FlightRouteId, cancellationToken);

        if (route is null)
        {
            return FlightDealErrors.RouteNotFound(deal.FlightRouteId);
        }

        try
        {
            var message = FlightDealMessageFactory.Create(deal, route, timeProvider.GetUtcNow().UtcDateTime);
            deal.MarkAsPublished(message.Id);
            messageRepository.Add(message);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return message.Id;
        }
        catch (DomainException exception)
        {
            return FlightDealErrors.InvalidState(exception.Message);
        }
    }
}

public sealed record DismissFlightDealCommand(Guid FlightDealId) : ICommand;

internal sealed class DismissFlightDealCommandHandler(IFlightDealRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<DismissFlightDealCommand>
{
    public async Task<Result> Handle(DismissFlightDealCommand command, CancellationToken cancellationToken)
    {
        var deal = await repository.GetByIdAsync(command.FlightDealId, cancellationToken);

        if (deal is null)
        {
            return FlightDealErrors.DealNotFound(command.FlightDealId);
        }

        try
        {
            deal.Dismiss();
        }
        catch (DomainException exception)
        {
            return FlightDealErrors.InvalidState(exception.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
