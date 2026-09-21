using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VisaTelegramBot.Domain.ChannelMessages;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.FlightDeals;

namespace VisaTelegramBot.Infrastructure.Persistence.Configurations;

internal sealed class FlightRouteConfiguration : IEntityTypeConfiguration<FlightRoute>
{
    public void Configure(EntityTypeBuilder<FlightRoute> builder)
    {
        builder.ToTable("FlightRoutes");

        builder.HasKey(route => route.Id).IsClustered(false);
        builder.Property(route => route.Id).ValueGeneratedNever();

        builder.HasIndex(route => new { route.CreatedAtUtc, route.Id })
            .IsUnique()
            .IsClustered()
            .HasDatabaseName("CIX_FlightRoutes_CreatedAtUtc_Id");

        builder.Property(route => route.Origin).AsAirportCode();
        builder.Property(route => route.Destination).AsAirportCode();
        builder.Property(route => route.Label).HasMaxLength(FlightRoute.LabelMaxLength).IsRequired();
        builder.Property(route => route.MaxPrice).HasPrecision(12, 2);
        builder.Property(route => route.CheckInterval).HasConversion<long>();
        builder.Property(route => route.LastError).HasMaxLength(FlightRoute.LastErrorMaxLength);

        builder.HasIndex(route => new { route.IsActive, route.NextCheckAtUtc })
            .HasDatabaseName("IX_FlightRoutes_IsActive_NextCheckAtUtc");

        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.Ignore(route => route.DomainEvents);
    }
}

internal sealed class FlightDealConfiguration : IEntityTypeConfiguration<FlightDeal>
{
    public void Configure(EntityTypeBuilder<FlightDeal> builder)
    {
        builder.ToTable("FlightDeals");

        builder.HasKey(deal => deal.Id).IsClustered(false);
        builder.Property(deal => deal.Id).ValueGeneratedNever();

        builder.HasIndex(deal => new { deal.FoundAtUtc, deal.Id })
            .IsUnique()
            .IsClustered()
            .HasDatabaseName("CIX_FlightDeals_FoundAtUtc_Id");

        builder.HasOne<FlightRoute>()
            .WithMany()
            .HasForeignKey(deal => deal.FlightRouteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ChannelMessage>()
            .WithMany()
            .HasForeignKey(deal => deal.ChannelMessageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(deal => deal.Origin).AsAirportCode();
        builder.Property(deal => deal.Destination).AsAirportCode();
        builder.Property(deal => deal.Price).HasPrecision(12, 2);
        builder.Property(deal => deal.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(deal => deal.Airline).HasMaxLength(FlightDeal.AirlineMaxLength);
        builder.Property(deal => deal.FlightNumber).HasMaxLength(FlightDeal.FlightNumberMaxLength);
        builder.Property(deal => deal.Status).HasConversion<int>();

        builder.Property(deal => deal.BookingUrl)
            .HasConversion(url => url.Value, value => WebUrl.Create(value))
            .HasMaxLength(WebUrl.MaxLength)
            .IsRequired();

        builder.Property(deal => deal.DealKey).HasMaxLength(FlightDeal.DealKeyMaxLength).IsUnicode(false).IsRequired();

        // Ayni firsatin iki kez kaydedilmesine karsi veritabani seviyesinde koruma.
        builder.HasIndex(deal => deal.DealKey).IsUnique().HasDatabaseName("UX_FlightDeals_DealKey");

        builder.HasIndex(deal => new { deal.Status, deal.FoundAtUtc }).HasDatabaseName("IX_FlightDeals_Status_FoundAtUtc");

        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.Ignore(deal => deal.DomainEvents);
    }
}

internal static class AirportCodePropertyExtensions
{
    public static PropertyBuilder<AirportCode> AsAirportCode(this PropertyBuilder<AirportCode> property)
    {
        return property
            .HasConversion(code => code.Value, value => AirportCode.Create(value))
            .HasMaxLength(AirportCode.Length)
            .IsFixedLength()
            .IsUnicode(false)
            .IsRequired();
    }
}
