using TransjapHorimetros.Application.Anomalies;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Application.Abstractions;

public interface IAnomalyRepository
{
    Task<Anomaly?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<RepositoryPage<Anomaly>> GetPageAsync(AnomalyQuery query, CancellationToken cancellationToken);

    Task<int> CountOpenAsync(Guid? machineId, CancellationToken cancellationToken);

    void Add(Anomaly anomaly);
}
