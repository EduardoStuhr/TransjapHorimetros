using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Readings;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.Abstractions;

public interface IReadingRepository
{
    Task<HourMeterReading?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<HourMeterReading?> GetByClientEventIdAsync(Guid clientEventId, CancellationToken cancellationToken);

    Task<HourMeterReading?> GetLatestValidBeforeAsync(
        Guid machineId,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HourMeterReading>> GetLatestByMachineIdsAsync(
        IReadOnlyCollection<Guid> machineIds,
        CancellationToken cancellationToken);

    Task<RepositoryPage<HourMeterReading>> GetPageAsync(
        ReadingQuery query,
        CancellationToken cancellationToken);

    Task<RepositoryPage<HourMeterReading>> GetForMachineAsync(
        Guid machineId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<int> CountByStatusAsync(ReadingStatus status, CancellationToken cancellationToken);

    Task<int> CountDistinctMachinesReceivedSinceAsync(
        DateTimeOffset receivedSince,
        CancellationToken cancellationToken);

    void Add(HourMeterReading reading);
}
