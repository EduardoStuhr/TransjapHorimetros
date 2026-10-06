using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Domain.Rules;

public sealed record ReadingAssessment(
    ReadingStatus Status,
    AnomalyType? AnomalyType,
    AnomalySeverity? Severity,
    string? Description)
{
    public static ReadingAssessment Validated { get; } = new(
        ReadingStatus.Validated,
        null,
        null,
        null);
}
