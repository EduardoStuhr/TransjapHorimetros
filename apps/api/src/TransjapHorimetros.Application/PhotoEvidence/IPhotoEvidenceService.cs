namespace TransjapHorimetros.Application.PhotoEvidence;

public interface IPhotoEvidenceService
{
    Task<UploadPhotoEvidenceResult> UploadAsync(
        Guid readingId,
        UploadPhotoEvidenceRequest request,
        string correlationId,
        CancellationToken cancellationToken,
        string? deviceSessionToken = null);

    Task<PhotoEvidenceResponse> GetMetadataAsync(
        Guid readingId,
        CancellationToken cancellationToken);

    Task<PhotoEvidenceContent> OpenContentAsync(
        Guid readingId,
        CancellationToken cancellationToken);
}
