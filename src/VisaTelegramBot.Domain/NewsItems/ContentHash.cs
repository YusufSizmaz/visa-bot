using System.Security.Cryptography;
using System.Text;
using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Domain.NewsItems;

/// <summary>
/// Bir haberin parmak izi. Ayni haberin iki kez kaydedilmesini (ve kanala iki kez basilmasini) engeller.
/// Veritabaninda unique index ile korunur.
/// </summary>
public sealed class ContentHash : ValueObject
{
    public const int Length = 64;

    private static readonly HashSet<string> TrackingParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "fbclid", "gclid", "mc_cid", "mc_eid", "ref", "igshid"
    };

    private ContentHash(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// Linkten parmak izi uretir. http/https farki, "www." oneki, sondaki "/" ve
    /// utm_* gibi takip parametreleri yok sayilir; boylece ayni haber farkli linklerle gelse de yakalanir.
    /// </summary>
    public static ContentHash FromUrl(WebUrl url)
    {
        ArgumentNullException.ThrowIfNull(url);

        var canonical = Canonicalize(url.ToUri());
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));

        return new ContentHash(Convert.ToHexStringLower(bytes));
    }

    /// <summary>Veritabanindan okunan hazir degeri dogrular.</summary>
    public static ContentHash FromValue(string? value)
    {
        if (value is null || value.Length != Length || !value.All(Uri.IsHexDigit))
        {
            throw new DomainException("Geçersiz içerik parmak izi.");
        }

        return new ContentHash(value.ToLowerInvariant());
    }

    private static string Canonicalize(Uri uri)
    {
        var host = uri.IdnHost.ToLowerInvariant();

        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";

        var path = uri.AbsolutePath.Length > 1
            ? uri.AbsolutePath.TrimEnd('/')
            : string.Empty;

        var query = string.Join('&', uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(parameter => !IsTrackingParameter(parameter)));

        return query.Length == 0
            ? $"{host}{port}{path}"
            : $"{host}{port}{path}?{query}";
    }

    private static bool IsTrackingParameter(string parameter)
    {
        var name = parameter.Split('=', 2)[0];

        return name.StartsWith("utm_", StringComparison.OrdinalIgnoreCase)
            || TrackingParameters.Contains(name);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
