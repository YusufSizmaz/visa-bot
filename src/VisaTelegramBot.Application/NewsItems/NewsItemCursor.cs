using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace VisaTelegramBot.Application.NewsItems;

/// <summary>
/// Keyset sayfalama imleci: son gorulen kaydin (kesif zamani, kimlik) ikilisi.
/// Istemciye opak bir metin olarak verilir; icerigine bagimli olmasin diye base64url kodlanir.
/// </summary>
public sealed record NewsItemCursor(DateTime DiscoveredAtUtc, Guid Id)
{
    public string Encode()
    {
        Span<byte> buffer = stackalloc byte[24];
        BinaryPrimitives.WriteInt64BigEndian(buffer, DiscoveredAtUtc.Ticks);
        Id.TryWriteBytes(buffer[8..]);

        return Convert.ToBase64String(buffer).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryDecode(string? value, [NotNullWhen(true)] out NewsItemCursor? cursor)
    {
        cursor = null;

        if (string.IsNullOrWhiteSpace(value) || value.Length != 32)
        {
            return false;
        }

        var base64 = value.Replace('-', '+').Replace('_', '/');
        Span<byte> buffer = stackalloc byte[24];

        if (!Convert.TryFromBase64String(base64, buffer, out var written) || written != 24)
        {
            return false;
        }

        var ticks = BinaryPrimitives.ReadInt64BigEndian(buffer);

        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
        {
            return false;
        }

        cursor = new NewsItemCursor(new DateTime(ticks, DateTimeKind.Utc), new Guid(buffer[8..]));
        return true;
    }
}
