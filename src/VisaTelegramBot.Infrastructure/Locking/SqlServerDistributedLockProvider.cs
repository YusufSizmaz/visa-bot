using System.Data;
using Microsoft.Data.SqlClient;
using VisaTelegramBot.Application.Abstractions.Locking;

namespace VisaTelegramBot.Infrastructure.Locking;

/// <summary>
/// SQL Server'in yerlesik uygulama kilidi (sp_getapplock) ile dagitik kilit.
/// Ek bir altyapi (Redis vb.) gerektirmez. Kilit, acik tutulan baglantiya (session) baglidir:
/// process coker ise baglanti kopar ve kilit otomatik olarak serbest kalir.
/// </summary>
internal sealed class SqlServerDistributedLockProvider(string connectionString) : IDistributedLockProvider
{
    private const int MaxResourceLength = 255;

    public async Task<IAsyncDisposable?> TryAcquireAsync(string resource, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        if (resource.Length > MaxResourceLength)
        {
            throw new ArgumentException($"Kilit adı en fazla {MaxResourceLength} karakter olabilir.", nameof(resource));
        }

        var connection = new SqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "sp_getapplock";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, MaxResourceLength) { Value = resource });
            command.Parameters.Add(new SqlParameter("@LockMode", SqlDbType.NVarChar, 32) { Value = "Exclusive" });
            command.Parameters.Add(new SqlParameter("@LockOwner", SqlDbType.NVarChar, 32) { Value = "Session" });
            command.Parameters.Add(new SqlParameter("@LockTimeout", SqlDbType.Int) { Value = 0 });

            var returnValue = new SqlParameter("@ReturnValue", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue };
            command.Parameters.Add(returnValue);

            await command.ExecuteNonQueryAsync(cancellationToken);

            // 0 ve 1 basari, negatif degerler kilidin alinamadigini gosterir.
            if (returnValue.Value is int code && code >= 0)
            {
                return new SqlServerLockHandle(connection, resource);
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

    private sealed class SqlServerLockHandle(SqlConnection connection, string resource) : IAsyncDisposable
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
                    command.CommandText = "sp_releaseapplock";
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, MaxResourceLength) { Value = resource });
                    command.Parameters.Add(new SqlParameter("@LockOwner", SqlDbType.NVarChar, 32) { Value = "Session" });
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException)
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
