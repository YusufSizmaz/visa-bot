using Microsoft.EntityFrameworkCore;
using VisaTelegramBot.Application.FlightDeals;
using VisaTelegramBot.Domain.FlightDeals;

namespace VisaTelegramBot.Infrastructure.Persistence.Repositories;

internal sealed class FlightRouteRepository(AppDbContext dbContext) : IFlightRouteRepository
{
    public Task<FlightRoute?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.FlightRoutes.FirstOrDefaultAsync(route => route.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetDueIdsAsync(DateTime utcNow, int limit, CancellationToken cancellationToken) =>
        await dbContext.FlightRoutes
            .AsNoTracking()
            .Where(route => route.IsActive && route.NextCheckAtUtc <= utcNow)
            .OrderBy(route => route.NextCheckAtUtc)
            .Select(route => route.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public void Add(FlightRoute route) => dbContext.FlightRoutes.Add(route);
}

internal sealed class FlightDealRepository(AppDbContext dbContext) : IFlightDealRepository
{
    public Task<FlightDeal?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.FlightDeals.FirstOrDefaultAsync(deal => deal.Id == id, cancellationToken);

    public async Task<IReadOnlySet<string>> GetExistingKeysAsync(IReadOnlyCollection<string> dealKeys, CancellationToken cancellationToken)
    {
        if (dealKeys.Count == 0)
        {
            return new HashSet<string>();
        }

        var keys = dealKeys.ToList();

        var existing = await dbContext.FlightDeals
            .AsNoTracking()
            .Where(deal => keys.Contains(deal.DealKey))
            .Select(deal => deal.DealKey)
            .ToListAsync(cancellationToken);

        return existing.ToHashSet(StringComparer.Ordinal);
    }

    public void AddRange(IEnumerable<FlightDeal> deals) => dbContext.FlightDeals.AddRange(deals);
}
