using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.Offline;

public sealed record OfflineReferenceDataResponse(
    int SchemaVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<OfflineMachineReference> Machines,
    IReadOnlyList<OfflineWorkSiteReference> WorkSites);

public sealed record OfflineMachineReference(
    Guid Id,
    int FleetNumber,
    string Model,
    MeterUnit? MeterUnit,
    MachineStatus Status,
    string? ActiveQrToken,
    DateTimeOffset UpdatedAt);

public sealed record OfflineWorkSiteReference(
    Guid Id,
    string Name,
    bool Active,
    DateTimeOffset UpdatedAt);
