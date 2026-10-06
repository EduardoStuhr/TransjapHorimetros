using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Domain.Entities;

public sealed class Anomaly
{
    private Anomaly()
    {
    }

    public Anomaly(
        Guid readingId,
        AnomalyType type,
        AnomalySeverity severity,
        string description,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        ReadingId = readingId;
        Type = type;
        Severity = severity;
        Description = description.Trim();
        Status = AnomalyStatus.Open;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public Guid ReadingId { get; private set; }

    public HourMeterReading Reading { get; private set; } = null!;

    public AnomalyType Type { get; private set; }

    public AnomalySeverity Severity { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public AnomalyStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
