using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Domain.FlightDeals;

/// <summary>Uc harfli IATA havalimani veya sehir kodu. Ornek: IST, SAW, MAD, BCN.</summary>
public sealed class AirportCode : ValueObject
{
    public const int Length = 3;

    private AirportCode(string value) => Value = value;

    public string Value { get; }

    public static AirportCode Create(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;

        if (normalized.Length != Length || !normalized.All(character => character is >= 'A' and <= 'Z'))
        {
            throw new DomainException($"Havalimanı kodu 3 harfli IATA kodu olmalı (ör. IST, MAD): '{value}'.");
        }

        return new AirportCode(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
