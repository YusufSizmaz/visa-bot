using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Domain.NewsItems;

namespace VisaTelegramBot.Infrastructure.Persistence.Repositories;

internal sealed class NewsItemRepository(AppDbContext dbContext) : INewsItemRepository
{
    /// <summary>
    /// "Competing consumers" icin SQL Server'daki klasik kuyruk sorgusu:
    /// UPDLOCK  satiri okurken kilitler, baska islem ayni satiri alamaz.
    /// READPAST kilitli satirlari beklemek yerine atlar; worker'lar birbirini bloklamaz.
    /// ROWLOCK  kilidi tablo yerine satir seviyesinde tutar.
    /// Secme ve isaretleme tek bir atomik UPDATE ile yapilir.
    /// </summary>
    private const string ClaimSql = """
        WITH claimable AS (
            SELECT TOP (@batchSize) [Id], [DeliveryLockedUntilUtc]
            FROM [NewsItems] WITH (UPDLOCK, READPAST, ROWLOCK)
            WHERE [DeliveryStatus] = @pendingStatus
              AND [NextDeliveryAttemptAtUtc] <= @utcNow
              AND ([DeliveryLockedUntilUtc] IS NULL OR [DeliveryLockedUntilUtc] < @utcNow)
            ORDER BY [DiscoveredAtUtc], [Id]
        )
        UPDATE claimable
        SET [DeliveryLockedUntilUtc] = @lockedUntilUtc
        OUTPUT inserted.[Id];
        """;

    public Task<NewsItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.NewsItems.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<IReadOnlySet<string>> GetExistingContentHashesAsync(
        IReadOnlyCollection<ContentHash> contentHashes,
        CancellationToken cancellationToken)
    {
        if (contentHashes.Count == 0)
        {
            return new HashSet<string>();
        }

        var hashes = contentHashes.ToList();

        var existing = await dbContext.NewsItems
            .AsNoTracking()
            .Where(item => hashes.Contains(item.ContentHash))
            .Select(item => item.ContentHash)
            .ToListAsync(cancellationToken);

        return existing.Select(hash => hash.Value).ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<Guid>> ClaimForDeliveryAsync(
        int batchSize,
        DateTime utcNow,
        DateTime lockedUntilUtc,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;

        if (openedHere)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ClaimSql;
            command.Parameters.Add(new SqlParameter("@batchSize", SqlDbType.Int) { Value = batchSize });
            command.Parameters.Add(new SqlParameter("@pendingStatus", SqlDbType.Int) { Value = (int)DeliveryStatus.Pending });
            command.Parameters.Add(new SqlParameter("@utcNow", SqlDbType.DateTime2) { Value = utcNow });
            command.Parameters.Add(new SqlParameter("@lockedUntilUtc", SqlDbType.DateTime2) { Value = lockedUntilUtc });

            var ids = new List<Guid>(batchSize);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }

            return ids;
        }
        finally
        {
            if (openedHere)
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }

    public void AddRange(IEnumerable<NewsItem> newsItems)
    {
        dbContext.NewsItems.AddRange(newsItems);
    }
}
