using TransjapHorimetros.Application.Security;

namespace TransjapHorimetros.Application.Abstractions;

public interface IAuthenticationService
{
    Task<AuthResponse> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string correlationId,
        CancellationToken cancellationToken);

    Task<AuthResponse> RefreshAsync(
        RefreshRequest request,
        string? ipAddress,
        string correlationId,
        CancellationToken cancellationToken);

    Task LogoutAsync(
        LogoutRequest? request,
        Guid? currentUserId,
        string? ipAddress,
        string correlationId,
        CancellationToken cancellationToken);

    Task<UserDto> GetMeAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
