using System.ComponentModel.DataAnnotations;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.Anomalies;

public sealed record AnomalyQuery
{
    public Guid? MachineId { get; init; }

    public AnomalyType? Type { get; init; }

    public AnomalyStatus? Status { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record AnomalyResponse(
    Guid Id,
    Guid ReadingId,
    Guid MachineId,
    int MachineFleetNumber,
    AnomalyType Type,
    AnomalySeverity Severity,
    string Description,
    AnomalyStatus Status,
    DateTimeOffset CreatedAt);
