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
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "A connection string 'Postgres' deve ser configurada por variável de ambiente ou appsettings.");

        services.AddDbContextPool<TransjapDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(TransjapDbContext).Assembly.FullName)));
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
