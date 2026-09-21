using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Testcontainers.MsSql;
using VisaTelegramBot.Infrastructure.Persistence;

namespace VisaTelegramBot.IntegrationTests.Persistence;

/// <summary>
/// Testler icin gercek bir SQL Server saglar. In-memory veritabani yerine gercek motor kullaniriz;
/// kilitler, unique index ve SQL cevirileri ancak boyle dogrulanir.
///
/// Oncelik sirasi:
/// 1. VISABOT_TEST_SQLSERVER ortam degiskeni tanimliysa o sunucu kullanilir (ornek: LocalDB).
/// 2. Docker calisiyorsa Testcontainers ile gecici bir SQL Server container'i baslatilir.
/// 3. Ikisi de yoksa testler "atlandi" olarak raporlanir.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        switch (SqlServerAvailability.Mode)
        {
            case SqlServerMode.External:
                ConnectionString = WithTestDatabase(SqlServerAvailability.ExternalConnectionString!);
                break;

            case SqlServerMode.Docker:
                _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
                await _container.StartAsync();
                ConnectionString = _container.GetConnectionString();
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
            .UseSqlServer(ConnectionString)
            .Options);

    internal static UnitOfWork CreateUnitOfWork(AppDbContext dbContext) =>
        new(dbContext, Substitute.For<IPublisher>(), NullLogger<UnitOfWork>.Instance);

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
        else if (SqlServerAvailability.Mode == SqlServerMode.External)
        {
            SqlConnection.ClearAllPools();
            await using var dbContext = CreateDbContext();
            await dbContext.Database.EnsureDeletedAsync();
        }
    }

    // Disaridan verilen sunucudaki gercek veritabanlarina dokunmamak icin testlere ozel bir veritabani adi kullanilir.
    private static string WithTestDatabase(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = $"VisaTelegramBot_Tests_{Guid.NewGuid():N}"
        };

        return builder.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (SqlServerAvailability.Mode == SqlServerMode.Unavailable)
        {
            Skip = "SQL Server yok: Docker çalışmıyor ve VISABOT_TEST_SQLSERVER tanımlı değil.";
        }
    }
}

internal enum SqlServerMode
{
    Unavailable,
    External,
    Docker
}

internal static class SqlServerAvailability
{
    private static readonly Lazy<SqlServerMode> LazyMode = new(Detect);

    public static string? ExternalConnectionString => Environment.GetEnvironmentVariable("VISABOT_TEST_SQLSERVER");

    public static SqlServerMode Mode => LazyMode.Value;

    private static SqlServerMode Detect()
    {
        if (!string.IsNullOrWhiteSpace(ExternalConnectionString))
        {
            return SqlServerMode.External;
        }

        return IsDockerRunning() ? SqlServerMode.Docker : SqlServerMode.Unavailable;
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
