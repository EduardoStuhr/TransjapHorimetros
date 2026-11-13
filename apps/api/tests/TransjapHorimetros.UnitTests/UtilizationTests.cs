using TransjapHorimetros.Application.Dashboard;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.UnitTests;

public sealed class UtilizationTests
{
    private static readonly DateTimeOffset Day = DateTimeOffset.Parse("2026-11-01T00:00:00Z");
    private static UtilizationPoint Point(int hour, decimal value, bool accepted = true,
        bool corrected = false, MeterUnit unit = MeterUnit.Hours, Guid? site = null) =>
        new(Guid.NewGuid(), Day.AddHours(hour), value, unit, site, accepted, corrected);

    [Fact]
    public void CapturedChronologyIgnoresArrivalOrderAndKeepsDecimalPrecision()
    {
        var result = Utilization.Calculate([Point(2, 100.22m), Point(0, 100.01m), Point(1, 100.11m)], Day, Day.AddDays(1));
        Assert.Equal("MISSING_INITIAL", result[0].Reason);
        Assert.Equal(0.21m, result.Sum(r => r.Difference ?? 0));
    }

    [Theory]
    [InlineData(false, false, 110, "REVIEW_REQUIRED")]
    [InlineData(true, true, 110, "CORRECTION_BOUNDARY")]
    [InlineData(true, false, 90, "NEGATIVE_OR_RESET")]
    public void UncertainBoundariesDoNotProduceHours(bool accepted, bool corrected, decimal value, string reason)
    {
        var result = Utilization.Calculate([Point(0, 100), Point(1, value, accepted, corrected)], Day, Day.AddDays(1));
        Assert.Equal(reason, result[1].Reason);
        Assert.Null(result[1].Difference);
    }

    [Fact]
    public void DuplicateTimestampInvalidatesBothAdjacentIntervals()
    {
        var result = Utilization.Calculate([Point(0, 100), Point(1, 110), Point(1, 110), Point(2, 120)], Day, Day.AddDays(1));
        Assert.All(result, r => Assert.Null(r.Difference));
    }

    [Fact]
    public void SiteChangeLeavesHoursUnattributedAndKilometersAreExcluded()
    {
        var result = Utilization.Calculate([Point(0, 100, site: Guid.NewGuid()), Point(1, 110, site: Guid.NewGuid()),
            Point(2, 120, unit: MeterUnit.Km)], Day, Day.AddDays(1));
        Assert.Equal(10m, result[1].Difference);
        Assert.Null(result[1].Site);
        Assert.Null(result[2].Difference);
    }

    [Fact]
    public void CrossingPeriodIsUnavailableAndEmptyPeriodHasNoIntervals()
    {
        var result = Utilization.Calculate([Point(0, 100), Point(2, 110)], Day.AddHours(1), Day.AddDays(1));
        Assert.Equal("CROSSES_PERIOD", result.Single().Reason);
        Assert.Empty(Utilization.Calculate([], Day, Day.AddDays(1)));
    }
}
