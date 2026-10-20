using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TransjapHorimetros.Infrastructure.Persistence.Migrations;
using TransjapHorimetros.Infrastructure.Persistence.Seed;
using TransjapHorimetros.Infrastructure.Security;

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

        try
        {
            var databaseCreator = dbContext.Database.GetService<IRelationalDatabaseCreator>();
            if (!await databaseCreator.ExistsAsync(cancellationToken))
            {
                logger.LogInformation("Banco de dados não encontrado. Criando banco inicial.");
                await databaseCreator.CreateAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Verificação de criação de banco ignorada ou banco já existente.");
        }

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

        var adminBootstrap = scope.ServiceProvider.GetRequiredService<AdminBootstrapService>();
        await adminBootstrap.SeedRolesAndAdminAsync(cancellationToken);

        logger.LogInformation("Banco migrado, seed e perfis de segurança concluídos.");
    }
}
