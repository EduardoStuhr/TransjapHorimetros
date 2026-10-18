using TransjapHorimetros.Application.Readings;

namespace TransjapHorimetros.Application.Abstractions;

public interface IReadingSummaryRepository
{
    Task<IReadOnlyDictionary<Guid, ReadingSummary>> GetAsync(IReadOnlyCollection<Guid> ids, bool detail, CancellationToken ct);
}
