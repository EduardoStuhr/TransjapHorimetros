namespace TransjapHorimetros.Domain.Enums;

public enum AnomalyType
{
    ReadingDecrease,
    ImpossibleHourIncrease,
    DuplicateReading,
    MissingReading,
    ClockSkew,
    LateSync,
    DayTransitionMismatch,
    LowOcrConfidence,
    OcrOperatorDivergence,
    WrongWorkSite,
}
