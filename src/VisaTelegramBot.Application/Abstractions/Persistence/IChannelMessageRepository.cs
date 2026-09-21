using VisaTelegramBot.Domain.ChannelMessages;

namespace VisaTelegramBot.Application.Abstractions.Persistence;

public interface IChannelMessageRepository
{
    Task<ChannelMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Zamani gelmis mesajlari atomik olarak kiralar (haberlerle ayni competing consumers deseni).</summary>
    Task<IReadOnlyList<Guid>> ClaimDueAsync(int batchSize, DateTime utcNow, DateTime lockedUntilUtc, CancellationToken cancellationToken);

    void Add(ChannelMessage message);
}
