using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Domain.ChannelMessages;
using VisaTelegramBot.Infrastructure.Persistence.Converters;

namespace VisaTelegramBot.Infrastructure.Persistence.Repositories;

internal sealed class ChannelMessageRepository(AppDbContext dbContext) : IChannelMessageRepository
{
    // NewsItemRepository'deki kuyruk sorgusuyla ayni desen: FOR UPDATE SKIP LOCKED + tek atomik UPDATE.
    private const string ClaimSql = """
        WITH claimable AS (
            SELECT "Id"
            FROM "ChannelMessages"
            WHERE "Status" = @scheduledStatus
              AND "ScheduledAtUtc" <= @utcNow
              AND ("LockedUntilUtc" IS NULL OR "LockedUntilUtc" < @utcNow)
            ORDER BY "ScheduledAtUtc", "CreatedAtUtc"
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED
        )
        UPDATE "ChannelMessages" AS message
        SET "LockedUntilUtc" = @lockedUntilUtc
        FROM claimable
        WHERE message."Id" = claimable."Id"
        RETURNING message."Id";
        """;

    public Task<ChannelMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ChannelMessages.FirstOrDefaultAsync(message => message.Id == id, cancellationToken);

    public void Add(ChannelMessage message) => dbContext.ChannelMessages.Add(message);

    public async Task<IReadOnlyList<Guid>> ClaimDueAsync(
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
            command.Parameters.Add(new NpgsqlParameter("scheduledStatus", NpgsqlDbType.Integer) { Value = (int)ChannelMessageStatus.Scheduled });
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
}
