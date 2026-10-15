using Microsoft.AspNetCore.Identity;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Application.Security;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Infrastructure.Identity;

namespace TransjapHorimetros.Infrastructure.Security;

public sealed class AuthenticationService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepository,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IAuthenticationService
{
    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new UnauthorizedException("Credenciais inválidas.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(normalizedEmail);

        if (user is null || !user.IsActive)
        {
            auditLogRepository.Add(new AuditLog(
                AuditActions.LoginFailed,
                nameof(ApplicationUser),
                normalizedEmail,
                null,
                "USUARIO_INEXISTENTE_OU_INATIVO",
                now,
                correlationId,
                user?.Id,
                normalizedEmail));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("Credenciais inválidas.");
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            auditLogRepository.Add(new AuditLog(
                AuditActions.LoginFailed,
                nameof(ApplicationUser),
                user.Id.ToString(),
                null,
                "CONTA_BLOQUEADA_LOCKOUT",
                now,
                correlationId,
                user.Id,
                user.Email));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("Conta temporariamente bloqueada por excesso de tentativas. Tente novamente mais tarde.");
        }

        if (!signInResult.Succeeded)
        {
            auditLogRepository.Add(new AuditLog(
                AuditActions.LoginFailed,
                nameof(ApplicationUser),
                user.Id.ToString(),
                null,
                "SENHA_INCORRETA",
                now,
                correlationId,
                user.Id,
                user.Email));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("Credenciais inválidas.");
        }

        user.LastLoginAt = now;
        await userManager.UpdateAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, accessExpiresAt) = tokenService.GenerateAccessToken(
            user.Id,
            user.Email!,
            user.FullName,
            roles);
        var (rawRefreshToken, tokenHash, refreshExpiresAt) = tokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken(
            user.Id,
            tokenHash,
            now,
            refreshExpiresAt,
            ipAddress);
        refreshTokenRepository.Add(refreshTokenEntity);

        auditLogRepository.Add(new AuditLog(
            AuditActions.LoginSuccess,
            nameof(ApplicationUser),
            user.Id.ToString(),
            null,
            null,
            now,
            correlationId,
            user.Id,
            user.Email));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            accessToken,
            accessExpiresAt,
            rawRefreshToken,
            ToUserDto(user, roles));
    }

    public async Task<AuthResponse> RefreshAsync(
        RefreshRequest request,
        string? ipAddress,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new UnauthorizedException("Token de atualização não informado.");
        }

        var tokenHash = tokenService.HashToken(request.RefreshToken);
        var currentToken = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (currentToken is null)
        {
            throw new UnauthorizedException("Token de atualização inválido ou não encontrado.");
        }

        if (currentToken.IsRevoked)
        {
            throw new UnauthorizedException("Token de atualização revogado. Faça login novamente.");
        }

        if (currentToken.IsExpired(now))
        {
            throw new UnauthorizedException("Token de atualização expirado. Faça login novamente.");
        }

        var user = await userManager.FindByIdAsync(currentToken.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException("Usuário inativo ou inexistente.");
        }

        var (newRawRefreshToken, newTokenHash, newRefreshExpiresAt) = tokenService.GenerateRefreshToken();
        var newRefreshToken = new RefreshToken(
            user.Id,
            newTokenHash,
            now,
            newRefreshExpiresAt,
            ipAddress);

        refreshTokenRepository.Add(newRefreshToken);
        currentToken.Revoke(now, ipAddress, newRefreshToken.Id);

        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, accessExpiresAt) = tokenService.GenerateAccessToken(
            user.Id,
            user.Email!,
            user.FullName,
            roles);

        auditLogRepository.Add(new AuditLog(
            AuditActions.RefreshTokenRotated,
            nameof(RefreshToken),
            newRefreshToken.Id.ToString(),
            currentToken.Id.ToString(),
            newRefreshToken.Id.ToString(),
            now,
            correlationId,
            user.Id,
            user.Email));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            accessToken,
            accessExpiresAt,
            newRawRefreshToken,
            ToUserDto(user, roles));
    }

    public async Task LogoutAsync(
        LogoutRequest? request,
        Guid? currentUserId,
        string? ipAddress,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            var tokenHash = tokenService.HashToken(request.RefreshToken);
            var token = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
            if (token is not null && !token.IsRevoked)
            {
                token.Revoke(now, ipAddress);
            }
        }

        if (currentUserId.HasValue)
        {
            var activeTokens = await refreshTokenRepository.GetActiveByUserIdAsync(
                currentUserId.Value,
                cancellationToken);
            foreach (var token in activeTokens)
            {
                token.Revoke(now, ipAddress);
            }

            auditLogRepository.Add(new AuditLog(
                AuditActions.Logout,
                nameof(ApplicationUser),
                currentUserId.Value.ToString(),
                null,
                null,
                now,
                correlationId,
                currentUserId.Value));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserDto> GetMeAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException("Usuário não encontrado ou inativo.");
        }

        var roles = await userManager.GetRolesAsync(user);
        return ToUserDto(user, roles);
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
