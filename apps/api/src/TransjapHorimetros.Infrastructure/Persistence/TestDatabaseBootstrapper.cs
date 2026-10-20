using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace TransjapHorimetros.Infrastructure.Persistence;

internal static class TestDatabaseBootstrapper
{
    private const int MaximumConnectionAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);
    private static readonly SemaphoreSlim BootstrapLock = new(1, 1);

    public static async Task EnsureCreatedAsync(
        string environmentName,
        string connectionString,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        TestDatabaseGuard.EnsureSafeTestDatabase(environmentName, connectionString);

        await BootstrapLock.WaitAsync(cancellationToken);
        try
        {
            var databaseBuilder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = databaseBuilder.InitialCatalog;
            var masterBuilder = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = "master",
                ConnectTimeout = databaseBuilder.ConnectTimeout == 0
                    ? 3
                    : Math.Min(databaseBuilder.ConnectTimeout, 3),
            };

            await using var connection = await OpenMasterConnectionAsync(
                masterBuilder.ConnectionString,
                logger,
                cancellationToken);

            if (await DatabaseExistsAsync(connection, databaseName, cancellationToken))
            {
                logger.LogInformation("Banco de integração {DatabaseName} já existe.", databaseName);
                return;
            }

            var quotedDatabaseName = new SqlCommandBuilder().QuoteIdentifier(databaseName);
            await using var createCommand = connection.CreateCommand();
            createCommand.CommandText = $"CREATE DATABASE {quotedDatabaseName}";

            try
            {
                await createCommand.ExecuteNonQueryAsync(cancellationToken);
                logger.LogInformation("Banco de integração {DatabaseName} criado.", databaseName);
            }
            catch (SqlException exception) when (exception.Number == 1801)
            {
                logger.LogInformation(
                    "Banco de integração {DatabaseName} foi criado por outra inicialização concorrente.",
                    databaseName);
            }
        }
        finally
        {
            BootstrapLock.Release();
        }
    }

    private static async Task<SqlConnection> OpenMasterConnectionAsync(
        string masterConnectionString,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumConnectionAttempts; attempt++)
        {
            var connection = new SqlConnection(masterConnectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
            catch (SqlException) when (attempt < MaximumConnectionAttempts)
            {
                await connection.DisposeAsync();
                logger.LogWarning(
                    "SQL Server ainda não está disponível para o bootstrap de integração (tentativa {Attempt}/{MaximumAttempts}).",
                    attempt,
                    MaximumConnectionAttempts);
                await Task.Delay(RetryDelay, cancellationToken);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        throw new InvalidOperationException("O SQL Server não ficou disponível para o bootstrap de integração.");
    }

    private static async Task<bool> DatabaseExistsAsync(
        SqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sys.databases WHERE [name] = @databaseName";
        command.Parameters.AddWithValue("@databaseName", databaseName);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }
}
