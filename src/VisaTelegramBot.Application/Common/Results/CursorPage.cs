namespace VisaTelegramBot.Application.Common.Results;

/// <summary>
/// Keyset (cursor) sayfalama sonucu. OFFSET/Skip yerine "son gordugum kayittan sonrasi" mantigi:
/// buyuk tablolarda sayfa numarasi arttikca yavaslamaz.
/// </summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
