using TransjapHorimetros.Application.Security;

namespace TransjapHorimetros.Application.Abstractions;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetAllAsync(
        CancellationToken cancellationToken);

    Task<UserDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<UserDto> CreateAsync(
        CreateUserRequest request,
        string correlationId,
        CancellationToken cancellationToken);

    Task<UserDto> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        string correlationId,
        CancellationToken cancellationToken);

    Task<UserDto> UpdateRolesAsync(
        Guid id,
        UpdateUserRolesRequest request,
        string correlationId,
        CancellationToken cancellationToken);

    Task<UserDto> ActivateAsync(
        Guid id,
        string correlationId,
        CancellationToken cancellationToken);

    Task<UserDto> DeactivateAsync(
        Guid id,
        string correlationId,
        CancellationToken cancellationToken);
}
