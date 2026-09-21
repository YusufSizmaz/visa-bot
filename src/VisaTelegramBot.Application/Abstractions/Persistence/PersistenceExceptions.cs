namespace VisaTelegramBot.Application.Abstractions.Persistence;

/// <summary>
/// Altyapiya ozgu hatalar (DbUpdateException, SqlException) Application katmanina bu tiplere cevrilerek gelir.
/// Boylece Application, EF Core'u tanimadan bu durumlari ele alabilir.
/// </summary>
public sealed class ConcurrencyConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class UniqueConstraintViolationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
