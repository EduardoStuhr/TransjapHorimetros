using System.ComponentModel.DataAnnotations;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.QrCodes;

public sealed record IssueMachineQrCodeRequest
{
    [StringLength(256)]
    public string? Token { get; init; }
}

public enum QrResolutionSource
{
    RegisteredQr,
    LegacyFleetNumber,
}

public sealed record QrResolutionResponse(
    Guid MachineId,
    int FleetNumber,
    string Model,
    MachineStatus Status,
    MeterUnit? MeterUnit,
    QrResolutionSource ResolutionSource,
    Guid? QrCodeId);

public sealed record MachineQrCodeResponse(
    Guid Id,
    Guid MachineId,
    string Token,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeactivatedAt);
