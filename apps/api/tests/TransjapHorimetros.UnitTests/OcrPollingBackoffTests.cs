using TransjapHorimetros.Application.Ocr;
using TransjapHorimetros.Infrastructure.Ocr;

namespace TransjapHorimetros.UnitTests;

public sealed class OcrPollingBackoffTests
{
    [Fact]
    public void IdleScansBackOffAndWorkRestoresResponsiveness()
    {
        var backoff = new OcrPollingBackoff(new OcrOptions());
        backoff.RecordSuccess(false);
        Assert.Equal(10, backoff.Delay.TotalSeconds);
        for (var i = 0; i < 20; i++) backoff.RecordSuccess(false);
        Assert.Equal(300, backoff.Delay.TotalSeconds);
        backoff.RecordSuccess(true);
        Assert.Equal(5, backoff.Delay.TotalSeconds);
    }

    [Fact]
    public void QuotaFailureWaitsAnHourAndTransientFailuresAreBounded()
    {
        var backoff = new OcrPollingBackoff(new OcrOptions());
        backoff.RecordFailure(42119);
        Assert.Equal(3600, backoff.Delay.TotalSeconds);
        backoff.RecordSuccess(true);
        for (var i = 0; i < 20; i++) backoff.RecordFailure(1205);
        Assert.Equal(300, backoff.Delay.TotalSeconds);
    }

    [Theory]
    [InlineData(0, 300, 3600)]
    [InlineData(300, 0, 3600)]
    [InlineData(300, 300, 0)]
    public void InvalidIntervalsAreRejected(int idle, int failure, int quota)
    {
        var options = new OcrOptions { MaximumIdlePollIntervalSeconds = idle,
            MaximumFailurePollIntervalSeconds = failure, QuotaRetryIntervalSeconds = quota };
        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
