using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VisaTelegramBot.Infrastructure.Persistence;

/// <summary>
/// "dotnet ef migrations add" komutu uygulamayi calistirmadan DbContext olusturabilsin diye.
/// Migration uretmek icin veritabanina baglanti gerekmez; baglanti sadece "database update" icin kullanilir.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string FallbackConnectionString =
        "Server=localhost,1433;Database=VisaTelegramBot;User Id=sa;Password=VisaBot_Dev_Passw0rd!;TrustServerCertificate=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database") ?? FallbackConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
