using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Application.Anomalies;

public sealed class AnomalyService(IAnomalyRepository anomalyRepository) : IAnomalyService
{
    public async Task<PagedResponse<AnomalyResponse>> GetPageAsync(
        AnomalyQuery query,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var result = await anomalyRepository.GetPageAsync(
            query with { Page = page, PageSize = pageSize },
            cancellationToken);
        var items = result.Items.Select(ToResponse).ToArray();
        return PagedResponse<AnomalyResponse>.Create(items, page, pageSize, result.TotalItems);
    }

    public async Task<AnomalyResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var anomaly = await anomalyRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("Anomalia", id);
        return ToResponse(anomaly);
    }

    private static AnomalyResponse ToResponse(Anomaly anomaly) =>
        new(
            anomaly.Id,
            anomaly.ReadingId,
            anomaly.MachineId,
            anomaly.Machine!.FleetNumber,
            anomaly.Type,
            anomaly.Severity,
            anomaly.Description,
            anomaly.Status,
            anomaly.CreatedAt);
}
