using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Domain.NewsItems;
using VisaTelegramBot.Infrastructure.Persistence.Converters;

namespace VisaTelegramBot.Infrastructure.Persistence.Repositories;

internal sealed class NewsItemRepository(AppDbContext dbContext) : INewsItemRepository
{
    /// <summary>
    /// "Competing consumers" icin PostgreSQL'deki klasik kuyruk sorgusu:
    /// FOR UPDATE      secilen satirlari kilitler, baska islem ayni satiri alamaz.
    /// SKIP LOCKED     kilitli satirlari beklemek yerine atlar; worker'lar birbirini bloklamaz.
    /// Secme ve isaretleme tek bir atomik UPDATE ile yapilir, alinan kimlikler RETURNING ile doner.
    /// </summary>
    private const string ClaimSql = """
        WITH claimable AS (
            SELECT "Id"
            FROM "NewsItems"
            WHERE "DeliveryStatus" = @pendingStatus
              AND "NextDeliveryAttemptAtUtc" <= @utcNow
              AND ("DeliveryLockedUntilUtc" IS NULL OR "DeliveryLockedUntilUtc" < @utcNow)
            ORDER BY "DiscoveredAtUtc", "Id"
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED
        )
        UPDATE "NewsItems" AS item
        SET "DeliveryLockedUntilUtc" = @lockedUntilUtc
        FROM claimable
        WHERE item."Id" = claimable."Id"
        RETURNING item."Id";
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
            command.Parameters.Add(new NpgsqlParameter("batchSize", NpgsqlDbType.Integer) { Value = batchSize });
            command.Parameters.Add(new NpgsqlParameter("pendingStatus", NpgsqlDbType.Integer) { Value = (int)DeliveryStatus.Pending });
            command.Parameters.Add(new NpgsqlParameter("utcNow", NpgsqlDbType.TimestampTz) { Value = UtcDateTime.Normalize(utcNow) });
            command.Parameters.Add(new NpgsqlParameter("lockedUntilUtc", NpgsqlDbType.TimestampTz) { Value = UtcDateTime.Normalize(lockedUntilUtc) });

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
