using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        logger.LogInformation("Aplicando migrations do SQL Server.");
        await dbContext.Database.MigrateAsync(cancellationToken);
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.SeedAsync(cancellationToken);
        logger.LogInformation("Banco migrado e seed concluído.");
    }
}
