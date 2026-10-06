namespace TransjapHorimetros.Domain.Enums;

public enum AnomalyType
{
    ReadingDecrease,
    ImpossibleHourIncrease,
    DuplicateReading,
    MissingReading,
    LateSync,
    LowOcrConfidence,
    OcrOperatorDivergence,
}
