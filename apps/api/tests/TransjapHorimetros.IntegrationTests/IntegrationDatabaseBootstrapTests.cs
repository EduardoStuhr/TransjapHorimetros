using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransjapHorimetros.Infrastructure.Persistence;

namespace TransjapHorimetros.IntegrationTests;

public sealed class IntegrationDatabaseBootstrapTests
{
    [Fact]
    public async Task ApplicationStart_WhenDatabaseDoesNotExist_CreatesMigratesAndSeedsDatabase()
    {
        var connectionString = GetSafeTestConnectionString();
        await DropTestDatabaseIfItExistsAsync(connectionString);

        await using var factory = new TransjapApiFactory();
        using var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertDatabaseIsMigratedAndSeededAsync(factory);
    }

    [Fact]
    public async Task ApplicationStart_WhenDatabaseAlreadyExists_RemainsIdempotent()
    {
        await using (var firstFactory = new TransjapApiFactory())
        {
            using var firstClient = firstFactory.CreateAnonymousClient();
            Assert.Equal(HttpStatusCode.OK, (await firstClient.GetAsync("/health/ready")).StatusCode);
            await AssertDatabaseIsMigratedAndSeededAsync(firstFactory);
        }

        await using var secondFactory = new TransjapApiFactory();
        using var secondClient = secondFactory.CreateAnonymousClient();

        Assert.Equal(HttpStatusCode.OK, (await secondClient.GetAsync("/health/ready")).StatusCode);
        await AssertDatabaseIsMigratedAndSeededAsync(secondFactory);
    }

    private static async Task AssertDatabaseIsMigratedAndSeededAsync(TransjapApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();

        Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());
        Assert.True(await dbContext.Machines.AnyAsync());
    }

    private static string GetSafeTestConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Defina TEST_SQLSERVER_CONNECTION_STRING para executar os testes de integração.");
        TestDatabaseGuard.EnsureSafeTestDatabase(
            TestDatabaseGuard.TestingEnvironmentName,
            connectionString,
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"));
        return connectionString;
    }

    private static async Task DropTestDatabaseIfItExistsAsync(string connectionString)
    {
        SqlConnection.ClearAllPools();
        var databaseBuilder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = databaseBuilder.InitialCatalog;
        databaseBuilder.InitialCatalog = "master";

        await using var connection = new SqlConnection(databaseBuilder.ConnectionString);
        await connection.OpenAsync();

        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "SELECT 1 FROM sys.databases WHERE [name] = @databaseName";
        existsCommand.Parameters.AddWithValue("@databaseName", databaseName);
        if (await existsCommand.ExecuteScalarAsync() is null)
        {
            return;
        }

        var quotedDatabaseName = new SqlCommandBuilder().QuoteIdentifier(databaseName);
        await using var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = $"""
            ALTER DATABASE {quotedDatabaseName} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE {quotedDatabaseName};
            """;
        await dropCommand.ExecuteNonQueryAsync();
        SqlConnection.ClearAllPools();
    }
}
