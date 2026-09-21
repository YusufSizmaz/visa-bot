using VisaTelegramBot.Application.Common.Results;

namespace VisaTelegramBot.Application.NewsItems;

public static class NewsItemErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("NewsItem.NotFound", $"'{id}' kimlikli haber bulunamadı.");

    public static Error InvalidState(string message) =>
        Error.Validation("NewsItem.InvalidState", message);
}
