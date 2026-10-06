using System.ComponentModel.DataAnnotations;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.Readings;

public sealed record ReadingQuery
{
    public Guid? MachineId { get; init; }

    public int? FleetNumber { get; init; }

    public Guid? WorkSiteId { get; init; }

    public ReadingStatus? Status { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record CreateReadingRequest
{
    public Guid MachineId { get; init; }

    public Guid? WorkSiteId { get; init; }

    [Range(0d, 9_999_999_999.99d)]
    public decimal Value { get; init; }

    [Required]
    public ReadingType? ReadingType { get; init; }

    public DateTimeOffset CapturedAtDevice { get; init; }

    public Guid ClientEventId { get; init; }
}

public sealed record ReadingResponse(
    Guid Id,
    Guid MachineId,
    int MachineFleetNumber,
    string MachineModel,
    Guid? WorkSiteId,
    string? WorkSiteName,
    decimal Value,
    ReadingType ReadingType,
    ReadingStatus Status,
    DateTimeOffset CapturedAtDevice,
    DateTimeOffset ReceivedAtServer,
    DateTimeOffset SyncedAt,
    Guid ClientEventId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateReadingResult(ReadingResponse Reading, bool IsExisting);
