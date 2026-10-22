using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.RegisteredDevices;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Application.Abstractions;

public interface IRegisteredDeviceRepository
{
    Task<RegisteredDevice?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);
    Task<RegisteredDevice?> GetByLocalDeviceIdAsync(string localDeviceId, bool trackChanges, CancellationToken cancellationToken);
    Task<RegisteredDevice?> GetByFingerprintAsync(string fingerprint, CancellationToken cancellationToken);
    Task<RepositoryPage<RegisteredDevice>> GetPageAsync(RegisteredDeviceQuery query, CancellationToken cancellationToken);
    Task<DeviceChallenge?> GetChallengeAsync(Guid challengeId, CancellationToken cancellationToken);
    Task<bool> TryConsumeChallengeAsync(Guid challengeId, DateTimeOffset consumedAt, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditLog>> GetHistoryAsync(Guid deviceId, CancellationToken cancellationToken);
    void Add(RegisteredDevice device);
    void Add(DeviceChallenge challenge);
}
