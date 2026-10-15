using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TransjapHorimetros.Domain.Security;
using TransjapHorimetros.Infrastructure.Identity;

namespace TransjapHorimetros.Infrastructure.Security;

public sealed class AdminBootstrapService(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<AdminBootstrapService> logger)
{
    public async Task SeedRolesAndAdminAsync(CancellationToken cancellationToken = default)
    {
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                logger.LogInformation("Criando papel de segurança '{Role}'.", role);
                var roleResult = await roleManager.CreateAsync(new ApplicationRole(role));
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Falha ao criar o papel '{role}': {string.Join(" ", roleResult.Errors.Select(error => error.Description))}");
                }
            }
        }

        var admins = await userManager.GetUsersInRoleAsync(AppRoles.Admin);
        if (admins.Count > 0)
        {
            return;
        }

        // O provider de variaveis de ambiente do .NET converte "__" para ":".
        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        var fullName = configuration["BootstrapAdmin:FullName"];

        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(fullName))
        {
            logger.LogInformation(
                "Nenhum usuário administrador cadastrado e as variáveis BootstrapAdmin__* não foram fornecidas. O primeiro administrador deverá ser provisionado por configuração.");
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existingUser = await userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser is null)
        {
            var adminUser = new ApplicationUser(normalizedEmail, fullName.Trim());
            var createResult = await userManager.CreateAsync(adminUser, password);

            if (!createResult.Succeeded)
            {
                logger.LogError(
                    "Falha ao provisionar o administrador inicial ({Email}): {Errors}",
                    normalizedEmail,
                    string.Join(" ", createResult.Errors.Select(error => error.Description)));
                return;
            }

            existingUser = adminUser;
        }

        if (!await userManager.IsInRoleAsync(existingUser, AppRoles.Admin))
        {
            var addRoleResult = await userManager.AddToRoleAsync(existingUser, AppRoles.Admin);
            if (!addRoleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Falha ao atribuir o papel ADMIN ao usuário inicial: {string.Join(" ", addRoleResult.Errors.Select(error => error.Description))}");
            }
        }

        logger.LogInformation(
            "Primeiro usuário administrador provisionado com sucesso para o e-mail {Email}. Remova as variáveis de ambiente BootstrapAdmin__* após o provisionamento.",
            normalizedEmail);
    }
}
