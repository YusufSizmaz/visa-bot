using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Testcontainers.PostgreSql;
using VisaTelegramBot.Infrastructure.Persistence;

namespace VisaTelegramBot.IntegrationTests.Persistence;

/// <summary>
/// Testler icin gercek bir PostgreSQL saglar. In-memory veritabani yerine gercek motor kullaniriz;
/// advisory lock'lar, unique index ve SQL cevirileri ancak boyle dogrulanir.
///
/// Oncelik sirasi:
/// 1. VISABOT_TEST_POSTGRES ortam degiskeni tanimliysa o sunucu kullanilir.
/// 2. Docker calisiyorsa Testcontainers ile gecici bir PostgreSQL container'i baslatilir.
/// 3. Ikisi de yoksa testler "atlandi" olarak raporlanir.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        switch (PostgresAvailability.Mode)
        {
            case PostgresMode.External:
                ConnectionString = WithTestDatabase(PostgresAvailability.ExternalConnectionString!);
                break;

            case PostgresMode.Docker:
                _container = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
                await _container.StartAsync();
                // Container'in varsayilan veritabanina degil, teste ozel bir veritabanina baglaniriz:
                // aksi halde asagidaki EnsureDeleted bagli oldugu veritabanini dusurmeye calisir.
                ConnectionString = WithTestDatabase(_container.GetConnectionString());
                break;

            default:
                return;
        }

        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    public AppDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    internal static UnitOfWork CreateUnitOfWork(AppDbContext dbContext) =>
        new(dbContext, Substitute.For<IPublisher>(), NullLogger<UnitOfWork>.Instance);

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        if (PostgresAvailability.Mode == PostgresMode.External)
        {
            // Acik havuz baglantilari kalirsa DROP DATABASE reddedilir.
            NpgsqlConnection.ClearAllPools();
            await using var dbContext = CreateDbContext();
            await dbContext.Database.EnsureDeletedAsync();
        }
    }

    // Disaridan verilen sunucudaki gercek veritabanlarina dokunmamak icin testlere ozel bir veritabani adi kullanilir.
    private static string WithTestDatabase(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = $"visabot_tests_{Guid.NewGuid():N}"
        };

        return builder.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (PostgresAvailability.Mode == PostgresMode.Unavailable)
        {
            Skip = "PostgreSQL yok: Docker çalışmıyor ve VISABOT_TEST_POSTGRES tanımlı değil.";
        }
    }
}

internal enum PostgresMode
{
    Unavailable,
    External,
    Docker
}

internal static class PostgresAvailability
{
    private static readonly Lazy<PostgresMode> LazyMode = new(Detect);

    public static string? ExternalConnectionString => Environment.GetEnvironmentVariable("VISABOT_TEST_POSTGRES");

    public static PostgresMode Mode => LazyMode.Value;

    private static PostgresMode Detect()
    {
        if (!string.IsNullOrWhiteSpace(ExternalConnectionString))
        {
            return PostgresMode.External;
        }

        return IsDockerRunning() ? PostgresMode.Docker : PostgresMode.Unavailable;
    }

    private static bool IsDockerRunning()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "version --format {{.Server.Version}}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null || !process.WaitForExit(15_000))
            {
                return false;
            }

            return process.ExitCode == 0 && process.StandardOutput.ReadToEnd().Trim().Length > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
