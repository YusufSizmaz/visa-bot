namespace VisaTelegramBot.Application.Common;

public static class TextTruncation
{
    private const char Ellipsis = '…';

    public static string Truncate(string value, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length <= maxLength)
        {
            return value;
        }

        return maxLength <= 1
            ? value[..maxLength]
            : string.Concat(value.AsSpan(0, maxLength - 1).TrimEnd(), Ellipsis.ToString());
    }
}
