using Microsoft.EntityFrameworkCore;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.RegisteredDevices;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Infrastructure.Persistence.Repositories;

public sealed class RegisteredDeviceRepository(TransjapDbContext dbContext) : IRegisteredDeviceRepository
{
    public Task<RegisteredDevice?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken)
    {
        var query = trackChanges ? dbContext.RegisteredDevices : dbContext.RegisteredDevices.AsNoTracking();
        return query.SingleOrDefaultAsync(device => device.Id == id, cancellationToken);
    }

    public Task<RegisteredDevice?> GetByLocalDeviceIdAsync(
        string localDeviceId, bool trackChanges, CancellationToken cancellationToken)
    {
        var query = trackChanges ? dbContext.RegisteredDevices : dbContext.RegisteredDevices.AsNoTracking();
        return query.SingleOrDefaultAsync(device => device.LocalDeviceId == localDeviceId, cancellationToken);
    }

    public Task<RegisteredDevice?> GetByFingerprintAsync(string fingerprint, CancellationToken cancellationToken) =>
        dbContext.RegisteredDevices.AsNoTracking()
            .SingleOrDefaultAsync(device => device.PublicKeyFingerprint == fingerprint, cancellationToken);

    public async Task<RepositoryPage<RegisteredDevice>> GetPageAsync(
        RegisteredDeviceQuery query, CancellationToken cancellationToken)
    {
        var devices = dbContext.RegisteredDevices.AsNoTracking().AsQueryable();
        if (query.Status.HasValue) devices = devices.Where(device => device.Status == query.Status.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = $"%{query.Search.Trim()}%";
            devices = devices.Where(device => EF.Functions.Like(device.DisplayName, search)
                || EF.Functions.Like(device.LocalDeviceId, search)
                || EF.Functions.Like(device.RequestedByEmail, search)
                || EF.Functions.Like(device.PublicKeyFingerprint, search));
        }

        var total = await devices.CountAsync(cancellationToken);
        var items = await devices.OrderByDescending(device => device.RequestedAt)
            .ThenByDescending(device => device.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new RepositoryPage<RegisteredDevice>(items, total);
    }

    public Task<DeviceChallenge?> GetChallengeAsync(Guid challengeId, CancellationToken cancellationToken) =>
        dbContext.DeviceChallenges.AsNoTracking().Include(challenge => challenge.RegisteredDevice)
            .SingleOrDefaultAsync(challenge => challenge.Id == challengeId, cancellationToken);

    public async Task<bool> TryConsumeChallengeAsync(
        Guid challengeId, DateTimeOffset consumedAt, CancellationToken cancellationToken) =>
        await dbContext.DeviceChallenges
            .Where(challenge => challenge.Id == challengeId
                && challenge.ConsumedAt == null
                && challenge.ExpiresAt > consumedAt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(challenge => challenge.ConsumedAt, consumedAt)
                .SetProperty(challenge => challenge.UpdatedAt, consumedAt), cancellationToken) == 1;

    public async Task<IReadOnlyList<AuditLog>> GetHistoryAsync(Guid deviceId, CancellationToken cancellationToken) =>
        await dbContext.AuditLogs.AsNoTracking()
            .Where(log => log.Entity == nameof(RegisteredDevice) && log.EntityId == deviceId.ToString())
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .ToListAsync(cancellationToken);

    public void Add(RegisteredDevice device) => dbContext.RegisteredDevices.Add(device);
    public void Add(DeviceChallenge challenge) => dbContext.DeviceChallenges.Add(challenge);
}
