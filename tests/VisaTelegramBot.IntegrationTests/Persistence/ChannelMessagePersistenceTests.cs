using Microsoft.EntityFrameworkCore;
using VisaTelegramBot.Domain.ChannelMessages;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.FlightDeals;
using VisaTelegramBot.Infrastructure.Persistence.Queries;
using VisaTelegramBot.Infrastructure.Persistence.Repositories;

namespace VisaTelegramBot.IntegrationTests.Persistence;

[Collection(SqlServerCollection.Name)]
public sealed class ChannelMessagePersistenceTests(SqlServerFixture fixture)
{
    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    [SqlServerFact]
    public async Task MessageWithPhotoAndButton_RoundTrips_AndListDoesNotNeedPhotoBytes()
    {
        var message = ChannelMessage.Create(
            ChannelMessageKind.Custom,
            "Duyuru",
            "İspanya randevuları açıldı.",
            WebUrl.Create("https://example.com/detay"),
            MessageButton.Create("Randevu al", WebUrl.Create("https://example.com/randevu")),
            MessagePhoto.Create(Png, "takvim.png"),
            Now.AddHours(3),
            Now);

        var plain = ChannelMessage.Create(ChannelMessageKind.Custom, null, "Görselsiz mesaj", null, null, null, null, Now);

        await using (var dbContext = fixture.CreateDbContext())
        {
            dbContext.ChannelMessages.AddRange(message, plain);
            await SqlServerFixture.CreateUnitOfWork(dbContext).SaveChangesAsync();
        }

        await using (var readContext = fixture.CreateDbContext())
        {
            var loaded = await readContext.ChannelMessages.SingleAsync(item => item.Id == message.Id);
            Assert.Equal("Randevu al", loaded.Button!.Text);
            Assert.Equal(Png, loaded.Photo!.Content);
            Assert.Equal("image/png", loaded.Photo.ContentType);

            var loadedPlain = await readContext.ChannelMessages.SingleAsync(item => item.Id == plain.Id);
            Assert.Null(loadedPlain.Photo);
            Assert.Null(loadedPlain.Button);

            var page = await new ChannelMessageQueries(readContext).ListAsync(new(null, 1, 100), CancellationToken.None);
            var listed = page.Items.Single(item => item.Id == message.Id);
            Assert.True(listed.HasPhoto);
            Assert.Equal("https://example.com/randevu", listed.ButtonUrl);

            var photo = await new ChannelMessageQueries(readContext).GetPhotoAsync(message.Id, CancellationToken.None);
            Assert.Equal(Png, photo!.Content);
        }
    }

    [SqlServerFact]
    public async Task Claim_ReturnsOnlyDueScheduledMessages()
    {
        var due = ChannelMessage.Create(ChannelMessageKind.Custom, null, "Şimdi", null, null, null, null, Now);
        var later = ChannelMessage.Create(ChannelMessageKind.Custom, null, "Sonra", null, null, null, Now.AddDays(1), Now);
        var cancelled = ChannelMessage.Create(ChannelMessageKind.Custom, null, "İptal", null, null, null, null, Now);
        cancelled.Cancel();

        await using (var dbContext = fixture.CreateDbContext())
        {
            dbContext.ChannelMessages.AddRange(due, later, cancelled);
            await SqlServerFixture.CreateUnitOfWork(dbContext).SaveChangesAsync();
        }

        await using var claimContext = fixture.CreateDbContext();
        var claimed = await new ChannelMessageRepository(claimContext).ClaimDueAsync(100, Now.AddMinutes(1), Now.AddMinutes(6), CancellationToken.None);

        Assert.Contains(due.Id, claimed);
        Assert.DoesNotContain(later.Id, claimed);
        Assert.DoesNotContain(cancelled.Id, claimed);
    }

    [SqlServerFact]
    public async Task FlightRouteAndDeal_RoundTrip_AndDealKeyIsUnique()
    {
        var route = FlightRoute.Create(
            AirportCode.Create("IST"), AirportCode.Create("MAD"), "İstanbul → Madrid", 3500m, 3, TimeSpan.FromHours(6), false, Now);

        var departure = new DateTimeOffset(2026, 10, 12, 6, 55, 0, TimeSpan.FromHours(3));
        var url = WebUrl.Create("https://www.aviasales.com/search/IST1210MAD1");

        await using (var dbContext = fixture.CreateDbContext())
        {
            dbContext.FlightRoutes.Add(route);
            dbContext.FlightDeals.Add(FlightDeal.Found(route.Id, route.Origin, route.Destination, departure, 2450m, "try", "PC", "1234", 0, url, Now));
            await SqlServerFixture.CreateUnitOfWork(dbContext).SaveChangesAsync();
        }

        await using (var readContext = fixture.CreateDbContext())
        {
            var deal = await readContext.FlightDeals.SingleAsync(item => item.FlightRouteId == route.Id);
            Assert.Equal(departure, deal.DepartureAt);
            Assert.Equal(2450m, deal.Price);
            Assert.Equal("MAD", deal.Destination.Value);

            var existing = await new FlightDealRepository(readContext).GetExistingKeysAsync([deal.DealKey], CancellationToken.None);
            Assert.Contains(deal.DealKey, existing);
        }

        await using var duplicateContext = fixture.CreateDbContext();
        duplicateContext.FlightDeals.Add(FlightDeal.Found(route.Id, route.Origin, route.Destination, departure, 2450m, "try", "PC", "1234", 0, url, Now.AddMinutes(1)));

        await Assert.ThrowsAsync<Application.Abstractions.Persistence.UniqueConstraintViolationException>(
            () => SqlServerFixture.CreateUnitOfWork(duplicateContext).SaveChangesAsync());
    }
}
