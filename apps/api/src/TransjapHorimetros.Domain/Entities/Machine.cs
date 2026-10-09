using TransjapHorimetros.Domain.Common;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Domain.Entities;

public sealed class Machine : AuditableEntity
{
    private Machine()
    {
    }

    public Machine(
        int fleetNumber,
        string model,
        MachineStatus status,
        DateTimeOffset now,
        MeterUnit? meterUnit = null)
    {
        Id = Guid.NewGuid();
        FleetNumber = fleetNumber;
        Model = model.Trim();
        Status = status;
        MeterUnit = meterUnit;
        CreatedAt = now.ToUniversalTime();
        UpdatedAt = CreatedAt;
    }

    public int FleetNumber { get; private set; }

    public string Model { get; private set; } = string.Empty;

    public MachineStatus Status { get; private set; }

    public MeterUnit? MeterUnit { get; private set; }

    public ICollection<HourMeterReading> Readings { get; } = new List<HourMeterReading>();

    public void Update(
        int fleetNumber,
        string model,
        MachineStatus status,
        MeterUnit? meterUnit,
        DateTimeOffset now)
    {
        FleetNumber = fleetNumber;
        Model = model.Trim();
        Status = status;
        MeterUnit = meterUnit;
        UpdatedAt = now.ToUniversalTime();
    }
}
