using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Domain.Rules;

namespace TransjapHorimetros.UnitTests;

public sealed class HourMeterReadingPolicyTests
{
    private readonly HourMeterReadingPolicy _policy = new();
    private static readonly DateTimeOffset BaseTime = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Assess_WhenCurrentValueIsLower_MarksReadingAsSuspect()
    {
        var result = _policy.Assess(100m, BaseTime, 90m, BaseTime.AddHours(1), 0.25m);

        Assert.Equal(ReadingStatus.Suspect, result.Status);
        Assert.Equal(AnomalyType.ReadingDecrease, result.AnomalyType);
    }

    [Fact]
    public void Assess_WhenIncreaseExceedsElapsedTime_MarksReadingAsSuspect()
    {
        var result = _policy.Assess(100m, BaseTime, 102m, BaseTime.AddHours(1), 0.25m);

        Assert.Equal(ReadingStatus.Suspect, result.Status);
        Assert.Equal(AnomalyType.ImpossibleHourIncrease, result.AnomalyType);
    }

    [Fact]
    public void Assess_WhenIncreaseIsPlausible_ValidatesReadingWithoutAnomaly()
    {
        var result = _policy.Assess(100m, BaseTime, 108.5m, BaseTime.AddHours(9), 0.25m);

        Assert.Equal(ReadingStatus.Validated, result.Status);
        Assert.Null(result.AnomalyType);
    }
}
