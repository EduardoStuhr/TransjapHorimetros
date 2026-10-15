using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Infrastructure.Identity;
using TransjapHorimetros.Infrastructure.Persistence;
using TransjapHorimetros.Infrastructure.Persistence.Repositories;
using TransjapHorimetros.Infrastructure.Persistence.Seed;
using TransjapHorimetros.Infrastructure.Security;

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

        services.AddDbContext<TransjapDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlServer => sqlServer
                    .MigrationsAssembly(typeof(TransjapDbContext).Assembly.FullName)
                    .EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null)));

        services.Configure<AuthenticationOptions>(
            configuration.GetSection(AuthenticationOptions.SectionName));

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredUniqueChars = 4;

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<TransjapDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

        services.AddScoped<TransjapRepository>();
        services.AddScoped<IMachineRepository>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IMachineQrCodeRepository>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IWorkSiteRepository>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IReadingRepository>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IAnomalyRepository>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IAuditLogRepository>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<AdminBootstrapService>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        return services;
    }
}
