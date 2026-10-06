using TransjapHorimetros.Application.Common;

namespace TransjapHorimetros.Application.Machines;

public interface IMachineService
{
    Task<PagedResponse<MachineListItemResponse>> GetPageAsync(
        MachineQuery query,
        CancellationToken cancellationToken);

    Task<MachineDetailsResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<MachineDetailsResponse> GetByFleetNumberAsync(int fleetNumber, CancellationToken cancellationToken);

    Task<MachineDetailsResponse> CreateAsync(
        CreateMachineRequest request,
        string correlationId,
        CancellationToken cancellationToken);

    Task<MachineDetailsResponse> UpdateAsync(
        Guid id,
        UpdateMachineRequest request,
        string correlationId,
        CancellationToken cancellationToken);
}
