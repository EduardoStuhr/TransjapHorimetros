using TransjapHorimetros.Application.Common;

namespace TransjapHorimetros.Application.Anomalies;

public interface IAnomalyService
{
    Task<PagedResponse<AnomalyResponse>> GetPageAsync(
        AnomalyQuery query,
        CancellationToken cancellationToken);

    Task<AnomalyResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
