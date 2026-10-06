using Npgsql;

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

        var testBuilder = new NpgsqlConnectionStringBuilder(testConnectionString);
        var testDatabase = testBuilder.Database;

        if (!string.Equals(testDatabase, ExpectedTestDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"OPERAÇÃO ABORTADA: O banco de testes deve se chamar estritamente '{ExpectedTestDatabaseName}', mas foi informado '{testDatabase}'. Proteção contra perda de dados ativada.");
        }

        if (!string.IsNullOrWhiteSpace(applicationConnectionString))
        {
            var appBuilder = new NpgsqlConnectionStringBuilder(applicationConnectionString);
            if (string.Equals(testDatabase, appBuilder.Database, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"OPERAÇÃO ABORTADA: A connection string de testes está apontando para o mesmo banco da aplicação ('{testDatabase}'). Proteção contra perda de dados ativada.");
            }
        }
    }
}
