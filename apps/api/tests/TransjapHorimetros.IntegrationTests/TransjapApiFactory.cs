using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TransjapHorimetros.Infrastructure.Persistence;
using TransjapHorimetros.Infrastructure.Persistence.Seed;

namespace TransjapHorimetros.IntegrationTests;

public sealed class TransjapApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string? _originalAppConnectionString;

    public TransjapApiFactory()
    {
        _connectionString =
            Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Defina TEST_SQLSERVER_CONNECTION_STRING para executar os testes de integração.");

        _originalAppConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        TestDatabaseGuard.EnsureSafeTestDatabase(_connectionString, _originalAppConnectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Database:ApplyMigrationsOnStartup"] = "true",
            });
        });
    }

    public async Task ResetDatabaseAsync()
    {
        TestDatabaseGuard.EnsureSafeTestDatabase(_connectionString, _originalAppConnectionString);

        _ = Server;
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM anomalies;
            DELETE FROM audit_logs;
            DELETE FROM hour_meter_readings;
            DELETE FROM work_sites;
            DELETE FROM machines;
            """);
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.SeedAsync();
    }
}
