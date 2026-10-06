using System.ComponentModel.DataAnnotations;
using TransjapHorimetros.Application.Readings;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.Machines;

public sealed record MachineQuery
{
    public int? FleetNumber { get; init; }

    [StringLength(160)]
    public string? Model { get; init; }

    public MachineStatus? Status { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record CreateMachineRequest
{
    [Range(1, int.MaxValue)]
    public int FleetNumber { get; init; }

    [Required]
    [StringLength(160, MinimumLength = 1)]
    public string Model { get; init; } = string.Empty;

    public MachineStatus Status { get; init; } = MachineStatus.Active;
}

public sealed record UpdateMachineRequest
{
    [Range(1, int.MaxValue)]
    public int FleetNumber { get; init; }

    [Required]
    [StringLength(160, MinimumLength = 1)]
    public string Model { get; init; } = string.Empty;

    public MachineStatus Status { get; init; }
}

public sealed record LatestReadingResponse(
    Guid Id,
    decimal Value,
    DateTimeOffset CapturedAtDevice,
    Guid? WorkSiteId,
    string? WorkSiteName);

public sealed record MachineListItemResponse(
    Guid Id,
    int FleetNumber,
    string Model,
    MachineStatus Status,
    LatestReadingResponse? LatestReading,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record MachineDetailsResponse(
    Guid Id,
    int FleetNumber,
    string Model,
    MachineStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ReadingResponse? LatestReading,
    IReadOnlyList<ReadingResponse> RecentReadings,
    int AlertCount,
    string? QrCode);
