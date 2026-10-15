namespace TransjapHorimetros.Application.Security;

public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    bool IsActive,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record RefreshRequest(
    string RefreshToken);

public sealed record LogoutRequest(
    string? RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    UserDto User);

public sealed record CreateUserRequest(
    string Email,
    string FullName,
    string Password,
    IReadOnlyList<string> Roles);

public sealed record UpdateUserRequest(
    string FullName);

public sealed record UpdateUserRolesRequest(
    IReadOnlyList<string> Roles);
