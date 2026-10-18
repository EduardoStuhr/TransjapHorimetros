using Microsoft.Extensions.Hosting;
using TransjapHorimetros.Application.Ocr;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Domain.Security;
using TransjapHorimetros.Infrastructure.Identity;
using TransjapHorimetros.Infrastructure.Persistence;
using TransjapHorimetros.Infrastructure.Persistence.Seed;

namespace TransjapHorimetros.IntegrationTests;

public sealed class TransjapApiFactory : WebApplicationFactory<Program>
{
    private const string TestSigningKey = "transjap-phase-3-integration-tests-signing-key-2026";
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

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Minimal hosting reads these settings before ConfigureWebHost callbacks are replayed.
        // DeferredHostBuilder passes host configuration into Program as command-line arguments.
        builder.ConfigureHostConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["environment"] = "Testing",
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Authentication:Jwt:SigningKey"] = TestSigningKey,
                ["Authentication:Jwt:Issuer"] = "TransjapHorimetrosTests",
                ["Authentication:Jwt:Audience"] = "TransjapHorimetrosTestsClient",
                ["Ocr:Provider"] = "Fake",
                ["Ocr:ReprocessCooldownSeconds"] = "1",
                ["Ocr:TimeoutSeconds"] = "1",
                ["Ocr:MaximumRetries"] = "0",
                ["RateLimiting:PermitLimit"] = "10000",
                ["RateLimiting:AuthLogin:PermitLimit"] = "10000",
            });
        });
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPhotoOcrProvider>();
            services.AddSingleton<FakePhotoOcrProvider>();
            services.AddSingleton<IPhotoOcrProvider>(p => p.GetRequiredService<FakePhotoOcrProvider>());
            services.RemoveAll<IPhotoStorage>();
            services.AddSingleton<InMemoryPhotoStorage>();
            services.AddSingleton<IPhotoStorage>(provider =>
                provider.GetRequiredService<InMemoryPhotoStorage>());
        });
    }

    public new HttpClient CreateClient() => CreateClientForRole(AppRoles.Admin);

    public HttpClient CreateClientForRole(string role)
    {
        var client = base.CreateClient();
        using var scope = Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var (token, _) = tokenService.GenerateAccessToken(
            Guid.NewGuid(),
            $"{role.ToLowerInvariant()}@tests.local",
            $"Teste {role}",
            [role]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateAnonymousClient() => base.CreateClient();

    public async Task<ApplicationUser> CreateUserAsync(
        string email,
        string password,
        bool isActive,
        params string[] roles)
    {
        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new ApplicationRole(role));
                EnsureIdentitySucceeded(roleResult);
            }
        }

        var user = new ApplicationUser(email, $"Usuario {roles.FirstOrDefault() ?? "Teste"}")
        {
            IsActive = isActive,
        };
        EnsureIdentitySucceeded(await userManager.CreateAsync(user, password));
        EnsureIdentitySucceeded(await userManager.AddToRolesAsync(user, roles));
        return user;
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
            DELETE FROM photo_ocr_results;
            DELETE FROM photo_evidence;
            DELETE FROM machine_qr_codes;
            DELETE FROM anomalies;
            DELETE FROM audit_logs;
            DELETE FROM refresh_tokens;
            DELETE FROM user_claims;
            DELETE FROM user_logins;
            DELETE FROM user_roles;
            DELETE FROM user_tokens;
            DELETE FROM users;
            DELETE FROM hour_meter_readings;
            DELETE FROM work_sites;
            DELETE FROM machines;
            """);
        scope.ServiceProvider.GetRequiredService<InMemoryPhotoStorage>().Clear();
        scope.ServiceProvider.GetRequiredService<FakePhotoOcrProvider>().Reset();
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.SeedAsync();
    }

    private static void EnsureIdentitySucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }
}
