using Microsoft.EntityFrameworkCore;
using VisaTelegramBot.Domain.ChannelMessages;
using VisaTelegramBot.Domain.FlightDeals;
using VisaTelegramBot.Domain.NewsItems;
using VisaTelegramBot.Domain.NewsSources;
using VisaTelegramBot.Infrastructure.Persistence.Converters;

namespace VisaTelegramBot.Infrastructure.Persistence;

/// <summary>
/// EF Core baglami. DbContext pooling ile kullanildigi icin constructor'a scoped servis enjekte edilmez;
/// domain event yayinlama isi bu yuzden UnitOfWork sinifinda.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<NewsSource> NewsSources => Set<NewsSource>();

    public DbSet<NewsItem> NewsItems => Set<NewsItem>();

    public DbSet<ChannelMessage> ChannelMessages => Set<ChannelMessage>();

    public DbSet<FlightRoute> FlightRoutes => Set<FlightRoute>();

    public DbSet<FlightDeal> FlightDeals => Set<FlightDeal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQL Server datetime2 saat dilimi bilgisi tutmaz. Okunan tum tarihleri UTC olarak isaretleriz.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
