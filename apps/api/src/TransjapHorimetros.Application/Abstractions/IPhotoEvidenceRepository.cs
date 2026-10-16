namespace TransjapHorimetros.Application.Abstractions;

public interface IPhotoEvidenceRepository
{
    Task<Domain.Entities.PhotoEvidence?> GetByReadingIdAsync(
        Guid readingId,
        CancellationToken cancellationToken);

    Task<Domain.Entities.PhotoEvidence?> GetByClientEventIdAsync(
        Guid clientEventId,
        CancellationToken cancellationToken);

    void Add(Domain.Entities.PhotoEvidence photoEvidence);
}
