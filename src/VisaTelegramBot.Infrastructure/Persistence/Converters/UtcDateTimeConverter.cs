using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace VisaTelegramBot.Infrastructure.Persistence.Converters;

/// <summary>
/// Tum DateTime degerlerinin veritabanina UTC olarak gitmesini ve UTC olarak geri okunmasini saglar.
/// PostgreSQL "timestamp with time zone" kolonuna Kind'i Utc olmayan bir deger yazilamaz.
/// </summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => UtcDateTime.Normalize(value),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

/// <summary>
/// EF disinda kalan ham ADO.NET sorgulari da ayni kurali uygulamak zorunda:
/// parametreler timestamptz kolonlariyla karsilastirildigi icin Kind'leri Utc olmalidir.
/// </summary>
internal static class UtcDateTime
{
    public static DateTime Normalize(DateTime value) =>
        value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
