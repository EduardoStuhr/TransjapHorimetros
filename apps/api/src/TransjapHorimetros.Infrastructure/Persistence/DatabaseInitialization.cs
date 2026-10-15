using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TransjapHorimetros.Infrastructure.Persistence.Migrations;
using TransjapHorimetros.Infrastructure.Persistence.Seed;

namespace TransjapHorimetros.Infrastructure.Persistence;

public static class DatabaseInitialization
{
    public static async Task MigrateAndSeedAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseInitialization");
        var dbContext = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pendingMigrations.Contains(ExistingMachineQrCodeDataPatch.MigrationId))
        {
            logger.LogInformation(
                "Preparando a frota antes da migration controlada dos QR codes físicos.");
            await dbContext.Database.MigrateAsync(
                ExistingMachineQrCodeDataPatch.PrerequisiteMigration,
                cancellationToken);
            await seeder.SeedAsync(cancellationToken);
        }

        logger.LogInformation("Aplicando migrations do SQL Server.");
        await dbContext.Database.MigrateAsync(cancellationToken);
        await seeder.SeedAsync(cancellationToken);
        logger.LogInformation("Banco migrado e seed concluído.");
    }
}
