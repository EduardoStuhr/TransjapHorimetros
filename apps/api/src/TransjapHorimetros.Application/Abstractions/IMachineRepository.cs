using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Machines;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Application.Abstractions;

public interface IMachineRepository
{
    Task<Machine?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);

    Task<Machine?> GetByFleetNumberAsync(int fleetNumber, bool trackChanges, CancellationToken cancellationToken);

    Task<bool> FleetNumberExistsAsync(int fleetNumber, Guid? excludingId, CancellationToken cancellationToken);

    Task<RepositoryPage<Machine>> GetPageAsync(MachineQuery query, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retorna os IDs de todas as máquinas com status ACTIVE.
    /// Usado pelo DashboardService para calcular máquinas sem leitura (RN-006).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetActiveIdsAsync(CancellationToken cancellationToken);

    void Add(Machine machine);
}
