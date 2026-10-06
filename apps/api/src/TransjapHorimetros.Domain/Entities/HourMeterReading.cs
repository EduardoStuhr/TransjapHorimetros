using TransjapHorimetros.Domain.Common;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Domain.Entities;

public sealed class HourMeterReading : AuditableEntity
{
    private HourMeterReading()
    {
    }

    public HourMeterReading(
        Guid machineId,
        Guid? workSiteId,
        decimal value,
        ReadingType readingType,
        ReadingStatus status,
        DateTimeOffset capturedAtDevice,
        DateTimeOffset receivedAtServer,
        DateTimeOffset syncedAt,
        Guid clientEventId)
    {
        Id = Guid.NewGuid();
        MachineId = machineId;
        WorkSiteId = workSiteId;
        Value = value;
        ReadingType = readingType;
        Status = status;
        CapturedAtDevice = capturedAtDevice.ToUniversalTime();
        ReceivedAtServer = receivedAtServer.ToUniversalTime();
        SyncedAt = syncedAt.ToUniversalTime();
        ClientEventId = clientEventId;
        CreatedAt = SyncedAt;
        UpdatedAt = SyncedAt;
    }

    public Guid MachineId { get; private set; }

    public Machine? Machine { get; private set; }

    public Guid? WorkSiteId { get; private set; }

    public WorkSite? WorkSite { get; private set; }

    public decimal Value { get; private set; }

    public ReadingType ReadingType { get; private set; }

    public ReadingStatus Status { get; private set; }

    public DateTimeOffset CapturedAtDevice { get; private set; }

    public DateTimeOffset ReceivedAtServer { get; private set; }

    public DateTimeOffset SyncedAt { get; private set; }

    public Guid ClientEventId { get; private set; }

    public ICollection<Anomaly> Anomalies { get; } = new List<Anomaly>();
}
