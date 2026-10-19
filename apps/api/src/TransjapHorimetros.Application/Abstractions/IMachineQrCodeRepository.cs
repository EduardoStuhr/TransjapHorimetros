using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Application.Abstractions;

public interface IMachineQrCodeRepository
{
    Task<IReadOnlyList<MachineQrCode>> GetActiveByMachineIdsAsync(
        IReadOnlyCollection<Guid> machineIds,
        CancellationToken cancellationToken);

    Task<MachineQrCode?> GetActiveByMachineIdAsync(
        Guid machineId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<MachineQrCode?> GetActiveByTokenAsync(string token, CancellationToken cancellationToken);

    Task<IReadOnlyList<MachineQrCode>> GetHistoryByMachineIdAsync(
        Guid machineId,
        CancellationToken cancellationToken);

    Task<bool> TokenExistsAsync(string token, CancellationToken cancellationToken);

    void Add(MachineQrCode machineQrCode);
}
