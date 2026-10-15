using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.UnitTests;

public sealed class MachineQrCodeTests
{
    [Fact]
    public void Constructor_CreatesActiveQrCode()
    {
        var machineId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var qrCode = new MachineQrCode(machineId, "TJ-ABC", now);

        Assert.NotEqual(Guid.Empty, qrCode.Id);
        Assert.Equal(machineId, qrCode.MachineId);
        Assert.Equal("TJ-ABC", qrCode.Token);
        Assert.True(qrCode.IsActive);
        Assert.Null(qrCode.DeactivatedAt);
        Assert.Equal(now, qrCode.CreatedAt);
        Assert.Equal(now, qrCode.UpdatedAt);
    }

    [Fact]
    public void Constructor_TrimsExternalWhitespace()
    {
        var qrCode = new MachineQrCode(Guid.NewGuid(), "  TJ-ABC  ", DateTimeOffset.UtcNow);

        Assert.Equal("TJ-ABC", qrCode.Token);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsEmptyToken(string token)
    {
        Assert.Throws<ArgumentException>(
            () => new MachineQrCode(Guid.NewGuid(), token, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_RejectsEmptyMachineId()
    {
        Assert.Throws<ArgumentException>(
            () => new MachineQrCode(Guid.Empty, "TJ-ABC", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_RejectsTokenLongerThanMaximum()
    {
        var token = new string('A', MachineQrCode.MaxTokenLength + 1);

        Assert.Throws<ArgumentException>(
            () => new MachineQrCode(Guid.NewGuid(), token, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Deactivate_MarksQrCodeInactiveAtServerTime()
    {
        var qrCode = new MachineQrCode(Guid.NewGuid(), "TJ-ABC", DateTimeOffset.UtcNow.AddHours(-1));
        var deactivatedAt = DateTimeOffset.UtcNow;

        qrCode.Deactivate(deactivatedAt);

        Assert.False(qrCode.IsActive);
        Assert.Equal(deactivatedAt, qrCode.DeactivatedAt);
        Assert.Equal(deactivatedAt, qrCode.UpdatedAt);
    }

    [Fact]
    public void Deactivate_WhenRepeated_PreservesOriginalDeactivationTime()
    {
        var qrCode = new MachineQrCode(Guid.NewGuid(), "TJ-ABC", DateTimeOffset.UtcNow.AddHours(-1));
        var firstDeactivation = DateTimeOffset.UtcNow;
        qrCode.Deactivate(firstDeactivation);

        qrCode.Deactivate(firstDeactivation.AddHours(1));

        Assert.Equal(firstDeactivation, qrCode.DeactivatedAt);
        Assert.Equal(firstDeactivation, qrCode.UpdatedAt);
    }

    [Fact]
    public void Timestamps_AreNormalizedToUtc()
    {
        var localTime = new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.FromHours(-3));
        var qrCode = new MachineQrCode(Guid.NewGuid(), "TJ-ABC", localTime);

        Assert.Equal(TimeSpan.Zero, qrCode.CreatedAt.Offset);
        Assert.Equal(localTime.UtcDateTime, qrCode.CreatedAt.UtcDateTime);

        qrCode.Deactivate(localTime.AddMinutes(10));
        Assert.Equal(TimeSpan.Zero, qrCode.DeactivatedAt!.Value.Offset);
    }

    [Fact]
    public void Deactivate_PreservesIdentityAndTokenForHistory()
    {
        var qrCode = new MachineQrCode(Guid.NewGuid(), "TJ-HISTORY", DateTimeOffset.UtcNow);
        var id = qrCode.Id;
        var token = qrCode.Token;

        qrCode.Deactivate(DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(id, qrCode.Id);
        Assert.Equal(token, qrCode.Token);
        Assert.NotNull(qrCode.DeactivatedAt);
    }
}
