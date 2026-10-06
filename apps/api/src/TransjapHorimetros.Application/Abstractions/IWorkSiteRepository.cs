using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Application.Abstractions;

public interface IWorkSiteRepository
{
    Task<WorkSite?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkSite>> GetAllAsync(bool? active, CancellationToken cancellationToken);

    void Add(WorkSite workSite);
}
