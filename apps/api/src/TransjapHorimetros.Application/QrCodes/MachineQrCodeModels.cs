using System.ComponentModel.DataAnnotations;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.QrCodes;

public sealed record IssueMachineQrCodeRequest
{
    [StringLength(256, MinimumLength = 1)]
    public string? Token { get; init; }
}

public sealed record MachineQrCodeResponse(
    Guid Id,
    Guid MachineId,
    string Token,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeactivatedAt);

public sealed record QrResolutionResponse(
    Guid MachineId,
    int FleetNumber,
    string Model,
    MachineStatus Status,
    MeterUnit? MeterUnit,
    string ResolutionSource,
    Guid? QrCodeId);
