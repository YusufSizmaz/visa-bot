using System.Data;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using VisaTelegramBot.Application.Abstractions.Locking;

namespace VisaTelegramBot.Infrastructure.Locking;

/// <summary>
/// PostgreSQL'in yerlesik danisma kilidi (advisory lock) ile dagitik kilit.
/// Ek bir altyapi (Redis vb.) gerektirmez. Kilit, acik tutulan baglantiya (session) baglidir:
/// process coker ise baglanti kopar ve kilit otomatik olarak serbest kalir.
/// </summary>
internal sealed class PostgresDistributedLockProvider(string connectionString) : IDistributedLockProvider
{
    private const int MaxResourceLength = 255;

    public async Task<IAsyncDisposable?> TryAcquireAsync(string resource, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        if (resource.Length > MaxResourceLength)
        {
            throw new ArgumentException($"Kilit adı en fazla {MaxResourceLength} karakter olabilir.", nameof(resource));
        }

        var key = ToLockKey(resource);
        var connection = new NpgsqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(@key)";
            command.Parameters.Add(new NpgsqlParameter("key", DbType.Int64) { Value = key });

            // pg_try_advisory_lock beklemez: kilit baskasindaysa hemen false doner.
            var acquired = await command.ExecuteScalarAsync(cancellationToken) is true;

            if (acquired)
            {
                return new PostgresLockHandle(connection, key);
            }

            await connection.DisposeAsync();
            return null;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Advisory lock anahtari metin degil 64 bit tamsayidir. Kaynak adinin SHA-256 ozetinin
    /// ilk 8 bayti kullanilir: ayni ad her zaman ayni anahtari verir, farkli adlarin cakismasi
    /// pratikte imkansizdir.
    /// </summary>
    internal static long ToLockKey(string resource)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(resource));

        return BitConverter.ToInt64(hash, 0);
    }

    private sealed class PostgresLockHandle(NpgsqlConnection connection, long key) : IAsyncDisposable
    {
        private int _disposed;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            try
            {
                if (connection.State == ConnectionState.Open)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = "SELECT pg_advisory_unlock(@key)";
                    command.Parameters.Add(new NpgsqlParameter("key", DbType.Int64) { Value = key });
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (NpgsqlException)
            {
                // Baglanti zaten koptuysa kilit sunucu tarafinda serbest kalmistir.
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
