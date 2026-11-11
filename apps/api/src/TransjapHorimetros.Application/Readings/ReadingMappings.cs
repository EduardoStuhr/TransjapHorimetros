using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Application.Readings;

internal static class ReadingMappings
{
    public static ReadingResponse ToResponse(
        HourMeterReading reading,
        Machine? machineOverride = null,
        WorkSite? workSiteOverride = null,
        ReadingSummary? summary = null,
        bool includeAdministrativeDetails = true)
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
            reading.UpdatedAt,
            reading.PhotoEvidence is not null,
            reading.MeterUnit,
            includeAdministrativeDetails ? reading.CreatedByUserId : null,
            includeAdministrativeDetails ? reading.CreatedByEmail : null,
            includeAdministrativeDetails ? reading.DeviceId : null,
            reading.PreviousReadingId,
            reading.DifferenceFromPrevious,
            includeAdministrativeDetails ? reading.CorrectionOfReadingId : null,
            includeAdministrativeDetails ? reading.RegisteredDeviceId : null)
        {
            AnomalyCount = summary?.AnomalyCount ?? 0,
            OpenAnomalyCount = summary?.OpenAnomalyCount ?? 0,
            CorrectionCount = summary?.CorrectionCount ?? 0,
            Ocr = includeAdministrativeDetails ? summary?.Ocr : null,
            Anomalies = includeAdministrativeDetails ? summary?.Anomalies : null,
            Corrections = includeAdministrativeDetails ? summary?.Corrections : null,
        };
    }
}
