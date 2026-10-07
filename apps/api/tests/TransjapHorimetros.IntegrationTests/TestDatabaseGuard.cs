using Microsoft.Data.SqlClient;

namespace TransjapHorimetros.IntegrationTests;

public static class TestDatabaseGuard
{
    public const string ExpectedTestDatabaseName = "transjap_horimetros_tests";

    public static void EnsureSafeTestDatabase(string testConnectionString, string? applicationConnectionString = null)
    {
        if (string.IsNullOrWhiteSpace(testConnectionString))
        {
            throw new InvalidOperationException("A connection string de teste não foi fornecida.");
        }

        var testBuilder = new SqlConnectionStringBuilder(testConnectionString);
        var testDatabase = testBuilder.InitialCatalog;

        if (!string.Equals(testDatabase, ExpectedTestDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"OPERAÇÃO ABORTADA: O banco de testes deve se chamar estritamente '{ExpectedTestDatabaseName}', mas foi informado '{testDatabase}'. Proteção contra perda de dados ativada.");
        }

        if (testBuilder.DataSource.Contains(".database.windows.net", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "OPERAÇÃO ABORTADA: Testes destrutivos não podem ser executados contra Azure SQL Database.");
        }

        if (!string.IsNullOrWhiteSpace(applicationConnectionString))
        {
            var appBuilder = new SqlConnectionStringBuilder(applicationConnectionString);
            if (string.Equals(testDatabase, appBuilder.InitialCatalog, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"OPERAÇÃO ABORTADA: A connection string de testes está apontando para o mesmo banco da aplicação ('{testDatabase}'). Proteção contra perda de dados ativada.");
            }
        }
    }
}
