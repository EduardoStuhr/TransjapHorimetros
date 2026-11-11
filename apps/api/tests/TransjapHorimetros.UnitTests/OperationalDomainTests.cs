using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.UnitTests;

public sealed class OperationalDomainTests
{
    [Fact]
    public void AlertReviewPreservesEvidenceAndPreventsReopeningResolvedAlert()
    {
        var alert = new Anomaly(Guid.NewGuid(), Guid.NewGuid(), AnomalyType.ReadingDecrease,
            AnomalySeverity.Warning, "Valor regressivo", DateTimeOffset.UtcNow);
        var description = alert.Description;
        alert.Review(AnomalyStatus.Acknowledged);
        alert.Review(AnomalyStatus.Resolved);
        Assert.Equal(description, alert.Description);
        Assert.Equal(AnomalyStatus.Resolved, alert.Status);
        Assert.Throws<InvalidOperationException>(() => alert.Review(AnomalyStatus.Open));
    }

    [Fact]
    public void AlertReviewRejectsRepeatedState()
    {
        var alert = new Anomaly(Guid.NewGuid(), Guid.NewGuid(), AnomalyType.ReadingDecrease,
            AnomalySeverity.Warning, "Valor regressivo", DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => alert.Review(AnomalyStatus.Open));
    }
}
