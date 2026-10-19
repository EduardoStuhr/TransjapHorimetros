using TransjapHorimetros.Application.Abstractions;

namespace TransjapHorimetros.Application.Offline;

public sealed class OfflineReferenceDataService(
    IMachineRepository machineRepository,
    IMachineQrCodeRepository qrCodeRepository,
    IWorkSiteRepository workSiteRepository,
    TimeProvider timeProvider) : IOfflineReferenceDataService
{
    public async Task<OfflineReferenceDataResponse> GetAsync(CancellationToken cancellationToken)
    {
        var machines = await machineRepository.GetActiveAsync(cancellationToken);
        var machineIds = machines.Select(machine => machine.Id).ToArray();
        var qrCodes = await qrCodeRepository.GetActiveByMachineIdsAsync(machineIds, cancellationToken);
        var qrByMachine = qrCodes.ToDictionary(qrCode => qrCode.MachineId);
        var workSites = await workSiteRepository.GetAllAsync(true, cancellationToken);

        return new OfflineReferenceDataResponse(
            SchemaVersion: 1,
            GeneratedAt: timeProvider.GetUtcNow(),
            Machines: machines.Select(machine => new OfflineMachineReference(
                machine.Id,
                machine.FleetNumber,
                machine.Model,
                machine.MeterUnit,
                machine.Status,
                qrByMachine.GetValueOrDefault(machine.Id)?.Token,
                machine.UpdatedAt)).ToArray(),
            WorkSites: workSites.Select(workSite => new OfflineWorkSiteReference(
                workSite.Id,
                workSite.Name,
                workSite.Active,
                workSite.UpdatedAt)).ToArray());
    }
}
