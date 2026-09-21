using FluentValidation;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.FlightDeals;

namespace VisaTelegramBot.Application.FlightDeals;

/// <param name="FlightRouteId">Null ise yeni rota olusturulur, degilse mevcut rota guncellenir.</param>
public sealed record SaveFlightRouteCommand(
    Guid? FlightRouteId,
    string Origin,
    string Destination,
    string? Label,
    decimal MaxPrice,
    int MonthsAhead,
    int CheckIntervalMinutes,
    bool AutoPublish) : ICommand<Guid>;

internal sealed class SaveFlightRouteCommandValidator : AbstractValidator<SaveFlightRouteCommand>
{
    public SaveFlightRouteCommandValidator()
    {
        RuleFor(x => x.Origin).Matches("^[A-Za-z]{3}$").WithMessage("Kalkış 3 harfli IATA kodu olmalı (ör. IST).");
        RuleFor(x => x.Destination).Matches("^[A-Za-z]{3}$").WithMessage("Varış 3 harfli IATA kodu olmalı (ör. MAD).");
        RuleFor(x => x.MaxPrice).GreaterThan(0).WithMessage("Azami fiyat sıfırdan büyük olmalı.");
        RuleFor(x => x.MonthsAhead).InclusiveBetween(1, FlightRoute.MaxMonthsAhead);
        RuleFor(x => x.CheckIntervalMinutes)
            .InclusiveBetween((int)FlightRoute.MinCheckInterval.TotalMinutes, (int)FlightRoute.MaxCheckInterval.TotalMinutes)
            .WithMessage("Kontrol aralığı 30 ile 1440 dakika arasında olmalı.");
        RuleFor(x => x.Label).MaximumLength(FlightRoute.LabelMaxLength);
    }
}

internal sealed class SaveFlightRouteCommandHandler(
    IFlightRouteRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SaveFlightRouteCommand, Guid>
{
    public async Task<Result<Guid>> Handle(SaveFlightRouteCommand command, CancellationToken cancellationToken)
    {
        FlightRoute? existing = null;

        if (command.FlightRouteId is { } id)
        {
            existing = await repository.GetByIdAsync(id, cancellationToken);

            if (existing is null)
            {
                return FlightDealErrors.RouteNotFound(id);
            }
        }

        FlightRoute route;

        try
        {
            var origin = AirportCode.Create(command.Origin);
            var destination = AirportCode.Create(command.Destination);
            var interval = TimeSpan.FromMinutes(command.CheckIntervalMinutes);

            if (existing is not null)
            {
                existing.Update(origin, destination, command.Label, command.MaxPrice, command.MonthsAhead, interval, command.AutoPublish);
                route = existing;
            }
            else
            {
                route = FlightRoute.Create(
                    origin, destination, command.Label, command.MaxPrice, command.MonthsAhead, interval, command.AutoPublish,
                    timeProvider.GetUtcNow().UtcDateTime);
                repository.Add(route);
            }
        }
        catch (DomainException exception)
        {
            return FlightDealErrors.InvalidState(exception.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return route.Id;
    }
}

public sealed record SetFlightRouteActiveCommand(Guid FlightRouteId, bool IsActive) : ICommand;

internal sealed class SetFlightRouteActiveCommandHandler(
    IFlightRouteRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SetFlightRouteActiveCommand>
{
    public async Task<Result> Handle(SetFlightRouteActiveCommand command, CancellationToken cancellationToken)
    {
        var route = await repository.GetByIdAsync(command.FlightRouteId, cancellationToken);

        if (route is null)
        {
            return FlightDealErrors.RouteNotFound(command.FlightRouteId);
        }

        if (command.IsActive)
        {
            route.Activate(timeProvider.GetUtcNow().UtcDateTime);
        }
        else
        {
            route.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
