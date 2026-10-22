namespace TransjapHorimetros.Application.Abstractions;

public sealed record DeviceSessionPrincipal(
    Guid RegisteredDeviceId,
    Guid UserId,
    DateTimeOffset ExpiresAt);

public interface IDeviceSessionTokenService
{
    (string Token, DateTimeOffset ExpiresAt) Create(Guid registeredDeviceId, Guid userId);
    DeviceSessionPrincipal? Validate(string token);
}
