using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsItems;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Infrastructure.Persistence.Configurations;

internal sealed class NewsItemConfiguration : IEntityTypeConfiguration<NewsItem>
{
    public void Configure(EntityTypeBuilder<NewsItem> builder)
    {
        builder.ToTable("NewsItems");

        // SQL Server uniqueidentifier degerlerini .NET'ten farkli bir bayt sirasiyla karsilastirir.
        // Bu yuzden Guid v7 bile SQL Server'da sirali degildir ve clustered anahtar olursa her insert
        // index'in ortasina duser (page split). Clustered index'i zaman sirali kolona veririz.
        builder.HasKey(item => item.Id).IsClustered(false);
        builder.Property(item => item.Id).ValueGeneratedNever();

        builder.HasIndex(item => new { item.DiscoveredAtUtc, item.Id })
            .IsUnique()
            .IsClustered()
            .HasDatabaseName("CIX_NewsItems_DiscoveredAtUtc_Id");

        // Aggregate'ler arasi iliski sadece yabanci anahtar olarak; navigation property yok.
        builder.HasOne<NewsSource>()
            .WithMany()
            .HasForeignKey(item => item.NewsSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(item => item.Title)
            .HasMaxLength(NewsItem.TitleMaxLength)
            .IsRequired();

        builder.Property(item => item.Url)
            .HasConversion(url => url.Value, value => WebUrl.Create(value))
            .HasMaxLength(WebUrl.MaxLength)
            .IsRequired();

        builder.Property(item => item.ContentHash)
            .HasConversion(hash => hash.Value, value => ContentHash.FromValue(value))
            .HasMaxLength(ContentHash.Length)
            .IsFixedLength()
            .IsUnicode(false)
            .IsRequired();

        // Ayni haberin iki kez kaydedilmesine karsi son savunma hatti. Uygulama kontrolu yarisa girebilir, index giremez.
        builder.HasIndex(item => item.ContentHash)
            .IsUnique()
            .HasDatabaseName("UX_NewsItems_ContentHash");

        builder.Property(item => item.Summary).HasMaxLength(NewsItem.SummaryMaxLength);
        builder.Property(item => item.DeliveryStatus).HasConversion<int>();
        builder.Property(item => item.ExternalMessageId).HasMaxLength(NewsItem.ExternalMessageIdMaxLength);
        builder.Property(item => item.LastDeliveryError).HasMaxLength(NewsItem.LastDeliveryErrorMaxLength);

        builder.Property<byte[]>("RowVersion").IsRowVersion();

        // Filtered index: sadece bekleyen haberleri icerir. Tablo milyonlarca satira ulassa da kucuk kalir.
        builder.HasIndex(item => item.NextDeliveryAttemptAtUtc)
            .HasFilter($"[{nameof(NewsItem.DeliveryStatus)}] = {(int)DeliveryStatus.Pending}")
            .IncludeProperties(item => new { item.DeliveryLockedUntilUtc })
            .HasDatabaseName("IX_NewsItems_Pending");

        builder.HasIndex(item => new { item.NewsSourceId, item.DiscoveredAtUtc })
            .HasDatabaseName("IX_NewsItems_NewsSourceId_DiscoveredAtUtc");

        builder.HasIndex(item => new { item.DeliveryStatus, item.DiscoveredAtUtc })
            .HasDatabaseName("IX_NewsItems_DeliveryStatus_DiscoveredAtUtc");

        builder.Ignore(item => item.DomainEvents);
    }
}
