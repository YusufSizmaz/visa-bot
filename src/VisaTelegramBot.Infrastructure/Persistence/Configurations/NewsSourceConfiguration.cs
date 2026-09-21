using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsSources;
using VisaTelegramBot.Infrastructure.Persistence.Converters;

namespace VisaTelegramBot.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent configuration: veritabani esleme kurallari domain sinifini kirletmeden burada tanimlanir.
/// Domain'de [Table], [MaxLength] gibi attribute'lar yoktur.
/// </summary>
internal sealed class NewsSourceConfiguration : IEntityTypeConfiguration<NewsSource>
{
    public void Configure(EntityTypeBuilder<NewsSource> builder)
    {
        builder.ToTable("NewsSources");

        builder.HasKey(source => source.Id);
        builder.Property(source => source.Id).ValueGeneratedNever();

        // Keyset (cursor) sayfalama bu sirayi kullanir; tekil index hem sirayi hem de
        // "ayni an + ayni id" ikiliginin olusmamasini garanti eder.
        builder.HasIndex(source => new { source.CreatedAtUtc, source.Id })
            .IsUnique()
            .HasDatabaseName("IX_NewsSources_CreatedAtUtc_Id");

        builder.Property(source => source.Name)
            .HasMaxLength(NewsSource.NameMaxLength)
            .IsRequired();

        builder.Property(source => source.Url)
            .HasConversion(url => url.Value, value => WebUrl.Create(value))
            .HasMaxLength(WebUrl.MaxLength)
            .IsRequired();

        builder.Property(source => source.Type).HasConversion<int>();

        builder.Property(source => source.ParsingRules)
            .HasConversion((ValueConverter)new HtmlParsingRulesJsonConverter())
            .HasMaxLength(HtmlParsingRulesJsonConverter.MaxLength);

        builder.Property(source => source.KeywordFilter)
            .HasConversion((ValueConverter)new KeywordFilterJsonConverter())
            .HasMaxLength(KeywordFilterJsonConverter.MaxLength);

        // Araligi tick olarak (bigint) sakliyoruz; boylece saglayiciya ozgu interval tipine bagimli kalmayiz.
        builder.Property(source => source.FetchInterval).HasConversion<long>();

        builder.Property(source => source.LastFetchError).HasMaxLength(NewsSource.LastFetchErrorMaxLength);

        // Optimistic concurrency: PostgreSQL'in her satirda tuttugu sistem kolonu "xmin" surum damgasi olarak kullanilir.
        // Ek bir kolon ve tetikleyici gerekmez; satir her guncellendiginde deger kendiliginden degisir.
        builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();

        builder.HasIndex(source => new { source.IsActive, source.NextFetchAtUtc })
            .HasDatabaseName("IX_NewsSources_IsActive_NextFetchAtUtc");

        builder.Ignore(source => source.DomainEvents);
        builder.Ignore(source => source.HasNeverSucceeded);
    }
}
