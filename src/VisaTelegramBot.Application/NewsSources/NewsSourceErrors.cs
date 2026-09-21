using VisaTelegramBot.Application.Common.Results;

namespace VisaTelegramBot.Application.NewsSources;

/// <summary>Bu ozelligin tum hata tanimlari tek yerde. Hata kodlari API istemcileri icin sabittir.</summary>
public static class NewsSourceErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("NewsSource.NotFound", $"'{id}' kimlikli kaynak bulunamadı.");

    public static readonly Error UrlAlreadyExists =
        Error.Conflict("NewsSource.UrlAlreadyExists", "Bu adresle kayıtlı bir kaynak zaten var.");

    public static Error InvalidState(string message) =>
        Error.Validation("NewsSource.InvalidState", message);

    public static Error FetchFailed(Guid id, string reason) =>
        Error.ExternalDependency("NewsSource.FetchFailed", $"'{id}' kimlikli kaynak okunamadı: {reason}");

    public static readonly Error ConcurrentFetch =
        Error.Conflict("NewsSource.ConcurrentFetch", "Aynı haberler başka bir işlem tarafından eş zamanlı kaydedildi.");
}
