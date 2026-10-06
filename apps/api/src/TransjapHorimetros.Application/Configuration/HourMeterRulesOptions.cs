namespace TransjapHorimetros.Application.Configuration;

public sealed class HourMeterRulesOptions
{
    public const string SectionName = "HourMeterRules";

    public decimal ElapsedTimeToleranceHours { get; init; } = 0.25m;
}
