using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Infrastructure.Persistence;
using TransjapHorimetros.Infrastructure.Persistence.Repositories;
using TransjapHorimetros.Infrastructure.Persistence.Seed;

namespace TransjapHorimetros.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "A connection string 'DefaultConnection' deve ser configurada por variável de ambiente ou appsettings.");

        services.AddDbContextPool<TransjapDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlServer => sqlServer
                    .MigrationsAssembly(typeof(TransjapDbContext).Assembly.FullName)
                    .EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null)));
        services.AddScoped<TransjapRepository>();
        services.AddScoped<IMachineRepository>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IWorkSiteRepository>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IReadingRepository>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IAnomalyRepository>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IAuditLogRepository>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        return services;
    }
}
