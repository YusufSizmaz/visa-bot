using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace VisaTelegramBot.Infrastructure.Persistence;

public static class MigrationExtensions
{
    /// <summary>
    /// Bekleyen migration'lari uygular. EF Core 9, ayni anda baslayan birden fazla kopyanin
    /// migration'i ayni anda calistirmasini veritabani kilidiyle engeller.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
