using Microsoft.EntityFrameworkCore;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Infrastructure.Persistence.Repositories;

internal sealed class NewsSourceRepository(AppDbContext dbContext) : INewsSourceRepository
{
    public Task<NewsSource?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.NewsSources.FirstOrDefaultAsync(source => source.Id == id, cancellationToken);
    }

    public Task<bool> ExistsByUrlAsync(WebUrl url, Guid? excludingId, CancellationToken cancellationToken)
    {
        return dbContext.NewsSources.AnyAsync(
            source => source.Url == url && (excludingId == null || source.Id != excludingId),
            cancellationToken);
    }

    public void Add(NewsSource newsSource)
    {
        dbContext.NewsSources.Add(newsSource);
    }
}
