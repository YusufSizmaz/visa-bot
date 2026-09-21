namespace VisaTelegramBot.Application.Common.Results;

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,

    /// <summary>Hata bizde degil, bagli oldugumuz dis bir sistemde (kaynak site, Telegram).</summary>
    ExternalDependency = 4
}

/// <summary>
/// Beklenen hata durumlarinin tipli temsili. Akis kontrolu icin exception firlatmak yerine Result icinde doner.
/// </summary>
public record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error ExternalDependency(string code, string message) => new(code, message, ErrorType.ExternalDependency);
}

/// <summary>Birden fazla alan hatasini tek hata olarak tasir.</summary>
public sealed record ValidationError(IReadOnlyCollection<Error> Errors)
    : Error("Validation.General", "Bir veya daha fazla doğrulama hatası oluştu.", ErrorType.Validation);
