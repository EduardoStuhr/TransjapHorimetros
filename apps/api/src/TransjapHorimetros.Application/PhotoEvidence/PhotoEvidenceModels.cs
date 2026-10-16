using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.PhotoEvidence;

public sealed record UploadPhotoEvidenceRequest(
    Stream Content,
    string OriginalFileName,
    string ContentType,
    DateTimeOffset? CapturedAtDevice,
    string? DeviceId,
    Guid? ClientEventId);

public sealed record PhotoEvidenceResponse(
    Guid Id,
    Guid ReadingId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    string Sha256,
    DateTimeOffset? CapturedAtDevice,
    DateTimeOffset UploadedAtServer,
    int? Width,
    int? Height,
    PhotoEvidenceStatus Status,
    Guid? CreatedByUserId,
    string? CreatedByEmail,
    DateTimeOffset CreatedAt,
    string? DeviceId,
    Guid? ClientEventId);

public sealed record UploadPhotoEvidenceResult(
    PhotoEvidenceResponse Evidence,
    bool IsExisting);

public sealed record PhotoEvidenceContent(
    Stream Content,
    string ContentType,
    long ContentLength,
    string FileName);
