using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Domain.ChannelMessages;

/// <summary>Mesajin altindaki tiklanabilir buton, ornegin "Randevu al".</summary>
public sealed class MessageButton : ValueObject
{
    public const int TextMaxLength = 40;

    private MessageButton(string text, WebUrl url)
    {
        Text = text;
        Url = url;
    }

    // EF Core icin.
    private MessageButton()
    {
        Text = string.Empty;
        Url = null!;
    }

    public string Text { get; private set; }

    public WebUrl Url { get; private set; }

    public static MessageButton Create(string? text, WebUrl url)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("Buton metni boş olamaz.");
        }

        var trimmed = text.Trim();

        if (trimmed.Length > TextMaxLength)
        {
            throw new DomainException($"Buton metni en fazla {TextMaxLength} karakter olabilir.");
        }

        return new MessageButton(trimmed, url);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Text;
        yield return Url;
    }
}
