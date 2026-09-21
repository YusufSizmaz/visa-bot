using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VisaTelegramBot.Domain.ChannelMessages;
using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Infrastructure.Persistence.Configurations;

internal sealed class ChannelMessageConfiguration : IEntityTypeConfiguration<ChannelMessage>
{
    public void Configure(EntityTypeBuilder<ChannelMessage> builder)
    {
        builder.ToTable("ChannelMessages");

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();

        builder.HasIndex(message => new { message.CreatedAtUtc, message.Id })
            .IsUnique()
            .HasDatabaseName("IX_ChannelMessages_CreatedAtUtc_Id");

        builder.Property(message => message.Kind).HasConversion<int>();
        builder.Property(message => message.Status).HasConversion<int>();
        builder.Property(message => message.Title).HasMaxLength(ChannelMessage.TitleMaxLength);
        builder.Property(message => message.Body).HasMaxLength(ChannelMessage.BodyMaxLength).IsRequired();
        builder.Property(message => message.ExternalMessageId).HasMaxLength(ChannelMessage.ExternalMessageIdMaxLength);
        builder.Property(message => message.LastError).HasMaxLength(ChannelMessage.LastErrorMaxLength);

        builder.Property(message => message.LinkUrl)
            .HasConversion(url => url!.Value, value => WebUrl.Create(value))
            .HasMaxLength(WebUrl.MaxLength);

        // Owned type: value object ayri bir tablo degil, ayni tablodaki kolonlardir (ButtonText, ButtonUrl).
        builder.OwnsOne(message => message.Button, button =>
        {
            button.Property(value => value.Text).HasColumnName("ButtonText").HasMaxLength(MessageButton.TextMaxLength).IsRequired();
            button.Property(value => value.Url)
                .HasColumnName("ButtonUrl")
                .HasConversion(url => url.Value, value => WebUrl.Create(value))
                .HasMaxLength(WebUrl.MaxLength)
                .IsRequired();
        });

        builder.OwnsOne(message => message.Photo, photo =>
        {
            photo.Property(value => value.Content).HasColumnName("PhotoContent").IsRequired();
            photo.Property(value => value.ContentType).HasColumnName("PhotoContentType").HasMaxLength(50).IsRequired();
            photo.Property(value => value.FileName).HasColumnName("PhotoFileName").HasMaxLength(MessagePhoto.FileNameMaxLength).IsRequired();
        });

        builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();

        builder.HasIndex(message => message.ScheduledAtUtc)
            .HasFilter($"\"{nameof(ChannelMessage.Status)}\" = {(int)ChannelMessageStatus.Scheduled}")
            .IncludeProperties(message => new { message.LockedUntilUtc })
            .HasDatabaseName("IX_ChannelMessages_Scheduled");

        builder.Ignore(message => message.DomainEvents);
    }
}
