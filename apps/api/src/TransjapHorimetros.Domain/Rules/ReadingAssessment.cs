using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Domain.Rules;

public sealed record AnomalyDetection(
    AnomalyType Type,
    AnomalySeverity Severity,
    string Description);

public sealed record ReadingAssessment(
    ReadingStatus Status,
    IReadOnlyList<AnomalyDetection> Anomalies,
    string? RejectionReason = null)
{
    public static ReadingAssessment Validated { get; } = new(
        ReadingStatus.Validated,
        []);

    public static ReadingAssessment Rejected(string reason) => new(
        ReadingStatus.Rejected,
        [],
        reason);
}
