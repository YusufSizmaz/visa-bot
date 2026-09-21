namespace VisaTelegramBot.Application.NewsSources;

internal static class NewsSourceCache
{
    public const string Tag = "news-sources";

    // Worker her cekimde kaynagin durumunu gunceller. Her cekimde cache temizlemek yuksek trafikte cache'i
    // anlamsiz kilar; bunun yerine kisa bir sure tutariz. Yonetim ekrani en fazla bu kadar eski veri gorur.
    public static readonly TimeSpan Expiration = TimeSpan.FromMinutes(1);

    public static readonly string[] Tags = [Tag];

    public static string ById(Guid id) => $"news-sources:by-id:{id:N}";

    public static string List(bool? isActive) => $"news-sources:list:{isActive?.ToString() ?? "all"}";
}
