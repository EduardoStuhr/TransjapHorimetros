using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Domain.Rules;

public static class AnomalySeverityPolicy
{
    public static AnomalySeverity For(AnomalyType type) => type switch
    {
        AnomalyType.DuplicateReading => AnomalySeverity.Info,
        AnomalyType.ReadingDecrease => AnomalySeverity.Warning,
        AnomalyType.ImpossibleHourIncrease => AnomalySeverity.Warning,
        AnomalyType.MissingReading => AnomalySeverity.Warning,
        AnomalyType.ClockSkew => AnomalySeverity.Warning,
        AnomalyType.LateSync => AnomalySeverity.Warning,
        AnomalyType.DayTransitionMismatch => AnomalySeverity.Warning,
        AnomalyType.LowOcrConfidence => AnomalySeverity.Warning,
        AnomalyType.OcrOperatorDivergence => AnomalySeverity.Warning,
        AnomalyType.WrongWorkSite => AnomalySeverity.Warning,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
