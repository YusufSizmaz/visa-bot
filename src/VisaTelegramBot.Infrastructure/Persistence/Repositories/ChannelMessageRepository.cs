using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Domain.ChannelMessages;

namespace VisaTelegramBot.Infrastructure.Persistence.Repositories;

internal sealed class ChannelMessageRepository(AppDbContext dbContext) : IChannelMessageRepository
{
    // NewsItemRepository'deki kuyruk sorgusuyla ayni desen: UPDLOCK + READPAST + tek atomik UPDATE.
    private const string ClaimSql = """
        WITH claimable AS (
            SELECT TOP (@batchSize) [Id], [LockedUntilUtc]
            FROM [ChannelMessages] WITH (UPDLOCK, READPAST, ROWLOCK)
            WHERE [Status] = @scheduledStatus
              AND [ScheduledAtUtc] <= @utcNow
              AND ([LockedUntilUtc] IS NULL OR [LockedUntilUtc] < @utcNow)
            ORDER BY [ScheduledAtUtc], [CreatedAtUtc]
        )
        UPDATE claimable
        SET [LockedUntilUtc] = @lockedUntilUtc
        OUTPUT inserted.[Id];
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
            command.Parameters.Add(new SqlParameter("@batchSize", SqlDbType.Int) { Value = batchSize });
            command.Parameters.Add(new SqlParameter("@scheduledStatus", SqlDbType.Int) { Value = (int)ChannelMessageStatus.Scheduled });
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
}
