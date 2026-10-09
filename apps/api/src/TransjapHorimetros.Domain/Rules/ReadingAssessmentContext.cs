using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Domain.Rules;

public sealed record ReadingAssessmentContext(
    decimal? PreviousValue,
    DateTimeOffset? PreviousCapturedAt,
    ReadingType? PreviousReadingType,
    decimal CurrentValue,
    DateTimeOffset CurrentCapturedAt,
    ReadingType CurrentReadingType,
    DateTimeOffset ReceivedAtServer,
    bool IsPotentialDuplicate);
