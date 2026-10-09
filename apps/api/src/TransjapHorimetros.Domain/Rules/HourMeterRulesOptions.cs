using System.ComponentModel.DataAnnotations;

namespace TransjapHorimetros.Domain.Rules;

public sealed class HourMeterRulesOptions
{
    public const string SectionName = "HourMeterRules";

    [Range(0d, double.MaxValue)]
    public decimal DayTransitionToleranceHours { get; init; } = 0.5m;

    [Range(0d, 24d)]
    public decimal MaxPlausibleDailyHours { get; init; } = 24m;

    [Range(0d, double.MaxValue)]
    public decimal MaxPlausibleHoursPerElapsedHour { get; init; } = 1m;

    [Range(0d, double.MaxValue)]
    public decimal ElapsedTimeToleranceHours { get; init; } = 0.25m;

    [Range(1, int.MaxValue)]
    public int MissingReadingThresholdDays { get; init; } = 2;

    [Range(0, int.MaxValue)]
    public int ClockSkewToleranceSeconds { get; init; } = 300;

    [Range(1, int.MaxValue)]
    public int GeofenceDefaultRadiusMeters { get; init; } = 500;

    [Range(0d, 1d)]
    public decimal OcrConfidenceThreshold { get; init; } = 0.85m;
}
