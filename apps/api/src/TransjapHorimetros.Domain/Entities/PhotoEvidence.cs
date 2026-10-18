using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Domain.Entities;

public sealed class PhotoEvidence
{
    private PhotoEvidence()
    {
    }

    public PhotoEvidence(
        Guid id,
        Guid readingId,
        string storageProvider,
        string storageContainer,
        string storageObjectKey,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        string sha256,
        DateTimeOffset? capturedAtDevice,
        DateTimeOffset uploadedAtServer,
        int width,
        int height,
        Guid? createdByUserId,
        string? createdByEmail,
        string? deviceId,
        Guid? clientEventId)
    {
        Id = id;
        ReadingId = readingId;
        StorageProvider = storageProvider;
        StorageContainer = storageContainer;
        StorageObjectKey = storageObjectKey;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        Sha256 = sha256;
        CapturedAtDevice = capturedAtDevice?.ToUniversalTime();
        UploadedAtServer = uploadedAtServer.ToUniversalTime();
        Width = width;
        Height = height;
        Status = PhotoEvidenceStatus.Active;
        CreatedByUserId = createdByUserId;
        CreatedByEmail = createdByEmail;
        CreatedAt = UploadedAtServer;
        DeviceId = deviceId;
        ClientEventId = clientEventId;
    }

    public Guid Id { get; private set; }

    public Guid ReadingId { get; private set; }

    public HourMeterReading? Reading { get; private set; }

    public string StorageProvider { get; private set; } = string.Empty;

    public string StorageContainer { get; private set; } = string.Empty;

    public string StorageObjectKey { get; private set; } = string.Empty;

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long FileSizeBytes { get; private set; }

    public string Sha256 { get; private set; } = string.Empty;

    public DateTimeOffset? CapturedAtDevice { get; private set; }

    public DateTimeOffset UploadedAtServer { get; private set; }

    public int? Width { get; private set; }

    public int? Height { get; private set; }

    public PhotoEvidenceStatus Status { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public string? CreatedByEmail { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? DeviceId { get; private set; }

    public Guid? ClientEventId { get; private set; }
}
