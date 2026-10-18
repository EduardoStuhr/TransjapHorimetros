using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.Readings;

public sealed record ReadingQuery
{
    public Guid? MachineId { get; init; }

    public int? FleetNumber { get; init; }

    public Guid? WorkSiteId { get; init; }

    public ReadingStatus? Status { get; init; }
    public MeterUnit? MeterUnit { get; init; }
    public ReadingType? ReadingType { get; init; }
    public bool? HasPhotoEvidence { get; init; }
    public bool? HasAnomaly { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateReadingRequest
{
    public Guid MachineId { get; init; }

    public Guid? WorkSiteId { get; init; }

    [Range(0d, 9_999_999_999.99d)]
    [JsonRequired]
    public decimal Value { get; init; }

    [Required]
    public ReadingType? ReadingType { get; init; }

    public DateTimeOffset CapturedAtDevice { get; init; }

    public Guid ClientEventId { get; init; }
    public string? DeviceId { get; init; }
    public Guid? CorrectionOfReadingId { get; init; }
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
    DateTimeOffset UpdatedAt,
    bool HasPhotoEvidence,
    MeterUnit? MeterUnit = null,
    Guid? CreatedByUserId = null,
    string? CreatedByEmail = null,
    string? DeviceId = null,
    Guid? PreviousReadingId = null,
    decimal? DifferenceFromPrevious = null,
    Guid? CorrectionOfReadingId = null)
{
    public int AnomalyCount { get; init; }
    public int OpenAnomalyCount { get; init; }
    public int CorrectionCount { get; init; }
    public ReadingOcrSummary? Ocr { get; init; }
    public IReadOnlyList<ReadingAnomalySummary>? Anomalies { get; init; }
    public IReadOnlyList<ReadingCorrectionSummary>? Corrections { get; init; }
}

public sealed record ReadingOcrSummary(PhotoOcrStatus Status, decimal? DetectedValue, decimal? Confidence, bool? MatchesOperatorValue);
public sealed record ReadingAnomalySummary(Guid Id, AnomalyType Type, AnomalyStatus Status, AnomalySeverity Severity, string Description);
public sealed record ReadingCorrectionSummary(Guid Id, decimal Value, ReadingStatus Status, DateTimeOffset CreatedAt);
public sealed record ReadingSummary(int AnomalyCount, int OpenAnomalyCount, int CorrectionCount,
    ReadingOcrSummary? Ocr, IReadOnlyList<ReadingAnomalySummary>? Anomalies = null,
    IReadOnlyList<ReadingCorrectionSummary>? Corrections = null);

public sealed record CreateReadingResult(ReadingResponse Reading, bool IsExisting);
