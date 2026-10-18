using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Domain.Rules;

namespace TransjapHorimetros.UnitTests;

public sealed class OperationalReadingTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static Machine Machine(MeterUnit? unit = MeterUnit.Hours) => new(999, "Test", MachineStatus.Active, Now, unit);
    private static HourMeterReading Create(Machine machine, decimal value = 100, HourMeterReading? previous = null,
        ReadingType type = ReadingType.Intermediate, HourMeterReading? original = null, string? deviceId = null) =>
        HourMeterReading.CreateOperational(machine, null, value, type,
            type == ReadingType.Correction ? ReadingStatus.Pending : ReadingStatus.Validated,
            Now, Now, Guid.NewGuid(), Guid.NewGuid(), "operator@tests.local", deviceId, previous, original);

    [Fact]
    public void FirstReading_HasServerAuthorSnapshotAndDistinctTimestamps()
    {
        var reading = Create(Machine(), deviceId: "  device-1  ");
        Assert.Equal(MeterUnit.Hours, reading.MeterUnit);
        Assert.NotNull(reading.CreatedByUserId);
        Assert.Equal("operator@tests.local", reading.CreatedByEmail);
        Assert.Equal("device-1", reading.DeviceId);
        Assert.Equal(Now, reading.CapturedAtDevice);
        Assert.Equal(Now, reading.ReceivedAtServer);
        Assert.Equal(reading.ReceivedAtServer, reading.SyncedAt);
        Assert.Null(reading.PreviousReadingId);
        Assert.Null(reading.DifferenceFromPrevious);
    }

    [Fact]
    public void Snapshot_DoesNotChangeWithMachineConfiguration()
    {
        var machine = Machine(); var reading = Create(machine);
        machine.Update(machine.FleetNumber, machine.Model, machine.Status, MeterUnit.Km, Now);
        Assert.Equal(MeterUnit.Hours, reading.MeterUnit);
    }

    [Fact]
    public void UnknownMachineUnit_IsRejectedWithoutInventingSnapshot() =>
        Assert.Throws<ArgumentException>(() => Create(Machine(null)));

    [Theory]
    [InlineData(101.25, 1.25)]
    [InlineData(100, 0)]
    [InlineData(90, -10)]
    public void Delta_UsesExactlyTheServerSelectedPreviousReading(decimal value, decimal expected)
    {
        var machine = Machine(); var previous = Create(machine);
        var current = Create(machine, value, previous);
        Assert.Equal(previous.Id, current.PreviousReadingId);
        Assert.Equal(expected, current.DifferenceFromPrevious);
        Assert.Equal(100, previous.Value);
    }

    [Fact]
    public void PreviousReading_CannotCrossMachineOrUnit()
    {
        var machine = Machine();
        Assert.Throws<ArgumentException>(() => Create(machine, previous: Create(Machine())));
        var old = Create(machine); machine.Update(999, "Test", MachineStatus.Active, MeterUnit.Km, Now);
        Assert.Throws<ArgumentException>(() => Create(machine, previous: old));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("10000000000")]
    [InlineData("123.456")]
    public void DecimalPrecision_IsRejectedInsteadOfRounded(string value) =>
        Assert.Throws<ArgumentException>(() => Create(Machine(), decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData(ReadingType.Opening)]
    [InlineData(ReadingType.Closing)]
    [InlineData(ReadingType.Intermediate)]
    [InlineData(ReadingType.Maintenance)]
    public void Types_ArePreserved(ReadingType type) => Assert.Equal(type, Create(Machine(), type: type).ReadingType);

    [Fact]
    public void Correction_IsPendingAndDoesNotChangeOriginal()
    {
        var machine = Machine(); var original = Create(machine);
        var correction = Create(machine, 99, type: ReadingType.Correction, original: original);
        Assert.Equal(original.Id, correction.CorrectionOfReadingId);
        Assert.Equal(ReadingStatus.Pending, correction.Status);
        Assert.Equal(100, original.Value);
        Assert.Equal(ReadingStatus.Validated, original.Status);
        Assert.Throws<ArgumentException>(() => Create(machine, type: ReadingType.Correction, original: correction));
        Assert.Throws<ArgumentException>(() => Create(machine, type: ReadingType.Correction));
        Assert.Throws<ArgumentException>(() => Create(machine, original: original));
    }

    [Theory]
    [InlineData(ReadingStatus.Pending, ReadingStatus.Validated, true)]
    [InlineData(ReadingStatus.Pending, ReadingStatus.Suspect, true)]
    [InlineData(ReadingStatus.Suspect, ReadingStatus.Validated, true)]
    [InlineData(ReadingStatus.Suspect, ReadingStatus.Rejected, true)]
    [InlineData(ReadingStatus.Validated, ReadingStatus.Corrected, true)]
    [InlineData(ReadingStatus.Rejected, ReadingStatus.Validated, false)]
    [InlineData(ReadingStatus.Corrected, ReadingStatus.Pending, false)]
    [InlineData(ReadingStatus.Validated, ReadingStatus.Validated, false)]
    public void StatusTransitions_AreCentralized(ReadingStatus from, ReadingStatus to, bool permitted)
    {
        Assert.Equal(permitted, ReadingStatusTransitionPolicy.CanTransition(from, to));
        if (permitted) ReadingStatusTransitionPolicy.EnsureTransition(from, to);
        else Assert.Throws<InvalidOperationException>(() => ReadingStatusTransitionPolicy.EnsureTransition(from, to));
    }

    [Fact]
    public void Device_IsBoundedAndControlCharactersAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Create(Machine(), deviceId: new string('x', 101)));
        Assert.Throws<ArgumentException>(() => Create(Machine(), deviceId: "a\nb"));
        Assert.Null(Create(Machine(), deviceId: "  ").DeviceId);
    }

    [Theory]
    [InlineData(MeterUnit.Hours, true)]
    [InlineData(MeterUnit.Km, false)]
    [InlineData(null, false)]
    public void ImpossibleHours_NeverAppliedToKilometersOrUnknownUnits(MeterUnit? unit, bool expected)
    {
        var result = Assess(unit, 120);
        Assert.Equal(expected, result.Anomalies.Any(a => a.Type == AnomalyType.ImpossibleHourIncrease));
    }

    [Theory]
    [InlineData(MeterUnit.Hours)]
    [InlineData(MeterUnit.Km)]
    public void Decrease_AppliesToBothUnits(MeterUnit unit) =>
        Assert.Contains(Assess(unit, 90).Anomalies, a => a.Type == AnomalyType.ReadingDecrease);

    [Fact]
    public void LateSync_IsOptInAndDoesNotTreatFutureCaptureAsLate()
    {
        var context = new ReadingAssessmentContext(null, null, null, 100, Now.AddHours(-2), ReadingType.Intermediate, Now, false, MeterUnit.Hours);
        var policy = new HourMeterReadingPolicy();
        Assert.DoesNotContain(policy.Assess(context, new()).Anomalies, a => a.Type == AnomalyType.LateSync);
        Assert.Contains(policy.Assess(context, new() { LateSyncThresholdSeconds = 3600 }).Anomalies, a => a.Type == AnomalyType.LateSync);
        Assert.DoesNotContain(policy.Assess(context with { CurrentCapturedAt = Now.AddHours(2) }, new() { LateSyncThresholdSeconds = 3600 }).Anomalies, a => a.Type == AnomalyType.LateSync);
    }

    [Fact]
    public void OperationalSequence_UsesKnownBoundariesWithoutInventingOpeningRequirement()
    {
        var policy = new HourMeterReadingPolicy();
        var context = new ReadingAssessmentContext(100, Now.AddHours(-1), ReadingType.Intermediate,
            100, Now, ReadingType.Opening, Now, false, MeterUnit.Hours, ReadingType.Opening);
        Assert.Contains(policy.Assess(context, new()).Anomalies, a => a.Type == AnomalyType.DayTransitionMismatch);
        var closing = context with { CurrentReadingType = ReadingType.Closing, PreviousBoundaryType = ReadingType.Closing };
        Assert.DoesNotContain(policy.Assess(closing, new()).Anomalies, a => a.Type == AnomalyType.DayTransitionMismatch);
        Assert.Contains(policy.Assess(closing, new() { RequireOpeningBeforeClosing = true }).Anomalies, a => a.Type == AnomalyType.DayTransitionMismatch);
    }

    private static ReadingAssessment Assess(MeterUnit? unit, decimal value) => new HourMeterReadingPolicy().Assess(
        new(100, Now.AddHours(-5), ReadingType.Intermediate, value, Now, ReadingType.Intermediate, Now, false, unit), new());

    [Theory]
    [InlineData(MeterUnit.Hours, true)]
    [InlineData(MeterUnit.Km, false)]
    public void OpeningAfterClosing_UsesBoundaryValueAcrossIntermediateReadings(MeterUnit unit, bool mismatch)
    {
        var context = new ReadingAssessmentContext(100.9m, Now.AddHours(-1), ReadingType.Intermediate,
            101, Now, ReadingType.Opening, Now, false, unit, ReadingType.Closing, 100, Now.AddHours(-2));
        var result = new HourMeterReadingPolicy().Assess(context, new());
        Assert.Equal(mismatch, result.Anomalies.Any(a => a.Type == AnomalyType.DayTransitionMismatch));
    }
}
