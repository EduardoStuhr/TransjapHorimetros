using TransjapHorimetros.Application.Common;

namespace TransjapHorimetros.Application.Readings;

public interface IReadingService
{
    Task<PagedResponse<ReadingResponse>> GetPageAsync(
        ReadingQuery query,
        CancellationToken cancellationToken);

    Task<ReadingResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResponse<ReadingResponse>> GetForMachineAsync(
        Guid machineId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<CreateReadingResult> CreateAsync(
        CreateReadingRequest request,
        string correlationId,
        CancellationToken cancellationToken);
}
