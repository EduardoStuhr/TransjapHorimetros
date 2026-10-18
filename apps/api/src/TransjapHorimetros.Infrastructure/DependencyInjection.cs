using TransjapHorimetros.Application.Ocr;
using TransjapHorimetros.Infrastructure.Ocr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.PhotoEvidence;
using TransjapHorimetros.Infrastructure.Identity;
using TransjapHorimetros.Infrastructure.Persistence;
using TransjapHorimetros.Infrastructure.Persistence.Repositories;
using TransjapHorimetros.Infrastructure.Persistence.Seed;
using TransjapHorimetros.Infrastructure.Security;
using TransjapHorimetros.Infrastructure.Storage;

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
        var photoEvidenceOptions = configuration
            .GetSection(PhotoEvidenceOptions.SectionName)
            .Get<PhotoEvidenceOptions>() ?? new PhotoEvidenceOptions();
        if (photoEvidenceOptions.MaximumFileSizeBytes <= 0
            || photoEvidenceOptions.MaximumFileSizeBytes > PhotoEvidenceOptions.AbsoluteMaximumFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"Storage:Photos:MaximumFileSizeBytes deve estar entre 1 e {PhotoEvidenceOptions.AbsoluteMaximumFileSizeBytes} bytes.");
        }
        services.AddSingleton(photoEvidenceOptions);
        var ocrOptions = configuration.GetSection("Ocr").Get<OcrOptions>() ?? new OcrOptions();
        ocrOptions.Validate();
        services.AddSingleton(ocrOptions);
        services.AddScoped<IPhotoOcrRepository, PhotoOcrRepository>();
        services.AddScoped<IPhotoOcrService, PhotoOcrService>();
        services.AddSingleton<IMeterReadingExtractor, MeterReadingExtractor>();
        if (ocrOptions.Provider == "AzureAiVision")
        {
            services.AddHttpClient<IPhotoOcrProvider, AzureAiVisionOcrProvider>(c => c.Timeout = Timeout.InfiniteTimeSpan)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        }
        else services.AddSingleton<IPhotoOcrProvider, FakePhotoOcrProvider>();

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
        services.AddScoped<IPhotoEvidenceRepository>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IAnomalyRepository>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IAuditLogRepository>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<TransjapRepository>());
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddSingleton<IPhotoStorage, AzureBlobPhotoStorage>();
        services.AddScoped<AdminBootstrapService>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        return services;
    }
}
