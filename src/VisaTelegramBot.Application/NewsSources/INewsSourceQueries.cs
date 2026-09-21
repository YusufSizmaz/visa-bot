namespace VisaTelegramBot.Application.NewsSources;

/// <summary>
/// CQRS okuma tarafi. Aggregate yuklemeden, dogrudan DTO'ya projeksiyon yapan ve takip (tracking) kullanmayan sorgular.
/// Yazma tarafindaki repository'den bilincli olarak ayridir.
/// </summary>
public interface INewsSourceQueries
{
    Task<NewsSourceResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<NewsSourceResponse>> ListAsync(bool? isActive, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetDueIdsAsync(DateTime utcNow, int limit, CancellationToken cancellationToken);
}
