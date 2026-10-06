using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Application.Readings;

internal static class ReadingMappings
{
    public static ReadingResponse ToResponse(
        HourMeterReading reading,
        Machine? machineOverride = null,
        WorkSite? workSiteOverride = null)
    {
        var machine = reading.Machine ?? machineOverride
            ?? throw new InvalidOperationException("A máquina relacionada não foi carregada.");
        var workSite = reading.WorkSite ?? workSiteOverride;

        return new ReadingResponse(
            reading.Id,
            reading.MachineId,
            machine.FleetNumber,
            machine.Model,
            reading.WorkSiteId,
            workSite?.Name,
            reading.Value,
            reading.ReadingType,
            reading.Status,
            reading.CapturedAtDevice,
            reading.ReceivedAtServer,
            reading.SyncedAt,
            reading.ClientEventId,
            reading.CreatedAt,
            reading.UpdatedAt);
    }
}
