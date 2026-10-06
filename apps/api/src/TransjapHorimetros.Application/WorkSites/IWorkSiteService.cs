namespace TransjapHorimetros.Application.WorkSites;

public interface IWorkSiteService
{
    Task<IReadOnlyList<WorkSiteResponse>> GetAllAsync(bool? active, CancellationToken cancellationToken);

    Task<WorkSiteResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<WorkSiteResponse> CreateAsync(
        CreateWorkSiteRequest request,
        string correlationId,
        CancellationToken cancellationToken);

    Task<WorkSiteResponse> UpdateAsync(
        Guid id,
        UpdateWorkSiteRequest request,
        string correlationId,
        CancellationToken cancellationToken);
}
