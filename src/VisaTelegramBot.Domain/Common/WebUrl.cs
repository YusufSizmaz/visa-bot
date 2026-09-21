using System.Diagnostics.CodeAnalysis;

namespace VisaTelegramBot.Domain.Common;

/// <summary>
/// Gecerli, mutlak bir http/https adresi. Primitive Obsession'dan kacinmak icin string yerine kullanilir.
/// Hem kaynak adreslerinde hem haber linklerinde ayni kural gecerli oldugu icin Common altinda.
/// </summary>
public sealed class WebUrl : ValueObject
{
    public const int MaxLength = 2048;

    private WebUrl(string value) => Value = value;

    public string Value { get; }

    public static WebUrl Create(string? value)
    {
        if (!TryCreate(value, out var url, out var error))
        {
            throw new DomainException(error);
        }

        return url;
    }

    public static bool TryCreate(
        string? value,
        [NotNullWhen(true)] out WebUrl? url,
        [NotNullWhen(false)] out string? error)
    {
        url = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Adres boş olamaz.";
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "Adres geçerli bir http veya https adresi olmalı.";
            return false;
        }

        var normalized = uri.AbsoluteUri;

        if (normalized.Length > MaxLength)
        {
            error = $"Adres en fazla {MaxLength} karakter olabilir.";
            return false;
        }

        url = new WebUrl(normalized);
        error = null;
        return true;
    }

    public Uri ToUri() => new(Value, UriKind.Absolute);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
