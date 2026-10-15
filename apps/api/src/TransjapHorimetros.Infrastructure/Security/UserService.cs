using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Application.Security;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Security;
using TransjapHorimetros.Infrastructure.Identity;

namespace TransjapHorimetros.Infrastructure.Security;

public sealed class UserService(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IRefreshTokenRepository refreshTokenRepository,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var users = await userManager.Users
            .OrderBy(user => user.FullName)
            .ToListAsync(cancellationToken);

        var list = new List<UserDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            list.Add(ToUserDto(user, roles));
        }

        return list;
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString())
            ?? throw new EntityNotFoundException("Usuário", id);

        var roles = await userManager.GetRolesAsync(user);
        return ToUserDto(user, roles);
    }

    public async Task<UserDto> CreateAsync(
        CreateUserRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BadRequestException("O e-mail é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new BadRequestException("O nome completo é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new BadRequestException("A senha é obrigatória.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existing = await userManager.FindByEmailAsync(normalizedEmail);
        if (existing is not null)
        {
            throw new ConflictException("Já existe um usuário cadastrado com este e-mail.");
        }

        var roles = ValidateAndNormalizeRoles(request.Roles);

        var user = new ApplicationUser(normalizedEmail, request.FullName);
        var createResult = await userManager.CreateAsync(user, request.Password);

        if (!createResult.Succeeded)
        {
            throw new BadRequestException(
                string.Join(" ", createResult.Errors.Select(error => error.Description)));
        }

        var addRolesResult = await userManager.AddToRolesAsync(user, roles);
        if (!addRolesResult.Succeeded)
        {
            throw new BadRequestException(
                string.Join(" ", addRolesResult.Errors.Select(error => error.Description)));
        }

        auditLogRepository.Add(new AuditLog(
            AuditActions.UserCreated,
            nameof(ApplicationUser),
            user.Id.ToString(),
            null,
            $"{{\"email\":\"{user.Email}\",\"fullName\":\"{user.FullName}\",\"roles\":[\"{string.Join("\",\"", roles)}\"]}}",
            now,
            correlationId,
            user.Id,
            user.Email));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToUserDto(user, roles);
    }

    public async Task<UserDto> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var user = await userManager.FindByIdAsync(id.ToString())
            ?? throw new EntityNotFoundException("Usuário", id);

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new BadRequestException("O nome completo é obrigatório.");
        }

        var oldFullName = user.FullName;
        user.FullName = request.FullName.Trim();
        user.UpdatedAt = now;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new BadRequestException(
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        auditLogRepository.Add(new AuditLog(
            AuditActions.UserUpdated,
            nameof(ApplicationUser),
            user.Id.ToString(),
            oldFullName,
            user.FullName,
            now,
            correlationId,
            user.Id,
            user.Email));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var roles = await userManager.GetRolesAsync(user);
        return ToUserDto(user, roles);
    }

    public async Task<UserDto> UpdateRolesAsync(
        Guid id,
        UpdateUserRolesRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var user = await userManager.FindByIdAsync(id.ToString())
            ?? throw new EntityNotFoundException("Usuário", id);

        var targetRoles = ValidateAndNormalizeRoles(request.Roles);
        var currentRoles = await userManager.GetRolesAsync(user);

        var rolesToRemove = currentRoles.Except(targetRoles, StringComparer.OrdinalIgnoreCase).ToArray();
        var rolesToAdd = targetRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToArray();

        if (rolesToRemove.Length > 0)
        {
            await userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }

        if (rolesToAdd.Length > 0)
        {
            await userManager.AddToRolesAsync(user, rolesToAdd);
        }

        user.UpdatedAt = now;
        await userManager.UpdateAsync(user);

        auditLogRepository.Add(new AuditLog(
            AuditActions.UserRolesChanged,
            nameof(ApplicationUser),
            user.Id.ToString(),
            string.Join(",", currentRoles),
            string.Join(",", targetRoles),
            now,
            correlationId,
            user.Id,
            user.Email));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToUserDto(user, targetRoles);
    }

    public async Task<UserDto> ActivateAsync(
        Guid id,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var user = await userManager.FindByIdAsync(id.ToString())
            ?? throw new EntityNotFoundException("Usuário", id);

        if (!user.IsActive)
        {
            user.IsActive = true;
            user.UpdatedAt = now;
            await userManager.UpdateAsync(user);

            auditLogRepository.Add(new AuditLog(
                AuditActions.UserActivated,
                nameof(ApplicationUser),
                user.Id.ToString(),
                "false",
                "true",
                now,
                correlationId,
                user.Id,
                user.Email));

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var roles = await userManager.GetRolesAsync(user);
        return ToUserDto(user, roles);
    }

    public async Task<UserDto> DeactivateAsync(
        Guid id,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var user = await userManager.FindByIdAsync(id.ToString())
            ?? throw new EntityNotFoundException("Usuário", id);

        if (user.IsActive)
        {
            user.IsActive = false;
            user.UpdatedAt = now;
            await userManager.UpdateAsync(user);

            var activeTokens = await refreshTokenRepository.GetActiveByUserIdAsync(id, cancellationToken);
            foreach (var token in activeTokens)
            {
                token.Revoke(now, "USER_DEACTIVATED");
            }

            auditLogRepository.Add(new AuditLog(
                AuditActions.UserDeactivated,
                nameof(ApplicationUser),
                user.Id.ToString(),
                "true",
                "false",
                now,
                correlationId,
                user.Id,
                user.Email));

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var roles = await userManager.GetRolesAsync(user);
        return ToUserDto(user, roles);
    }

    private static IReadOnlyList<string> ValidateAndNormalizeRoles(IReadOnlyList<string>? requestedRoles)
    {
        if (requestedRoles is null || requestedRoles.Count == 0)
        {
            throw new BadRequestException("O usuário deve possuir pelo menos um papel atribuído.");
        }

        var normalized = requestedRoles
            .Select(role => role.Trim().ToUpperInvariant())
            .Distinct()
            .ToArray();

        foreach (var role in normalized)
        {
            if (!AppRoles.IsValid(role))
            {
                throw new BadRequestException($"O papel '{role}' informado é inválido. Papéis válidos: {string.Join(", ", AppRoles.All)}.");
            }
        }

        return normalized;
    }

    private static UserDto ToUserDto(ApplicationUser user, IEnumerable<string> roles) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.IsActive,
            roles.ToArray(),
            user.CreatedAt,
            user.LastLoginAt);
}
