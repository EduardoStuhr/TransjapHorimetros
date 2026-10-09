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

    /// <summary>
    /// Verifica se já existe outra leitura da mesma máquina com valor e horário de captura idênticos
    /// mas com ClientEventId diferente. Usado para detectar DUPLICATE_READING (RN-005).
    /// </summary>
    Task<bool> ExistsSimilarAsync(
        Guid machineId,
        decimal value,
        DateTimeOffset capturedAt,
        Guid excludingClientEventId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Conta máquinas ativas que não tiveram nenhuma leitura recebida desde a data informada.
    /// Usado para calcular a métrica MissingReadingThresholdDays do dashboard (RN-006).
    /// </summary>
    Task<int> CountActiveMachinesWithoutReadingSinceAsync(
        IReadOnlyCollection<Guid> activeMachineIds,
        DateTimeOffset receivedSince,
        CancellationToken cancellationToken);

    void Add(HourMeterReading reading);
}
