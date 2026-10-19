namespace TransjapHorimetros.Application.Offline;

public interface IOfflineReferenceDataService
{
    Task<OfflineReferenceDataResponse> GetAsync(CancellationToken cancellationToken);
}
