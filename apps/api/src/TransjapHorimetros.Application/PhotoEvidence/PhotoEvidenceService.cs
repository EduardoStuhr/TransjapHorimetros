using System.Text.Json;
using Microsoft.Extensions.Logging;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Application.RegisteredDevices;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Application.PhotoEvidence;

public sealed class PhotoEvidenceService(
    IReadingRepository readingRepository,
    IPhotoEvidenceRepository photoEvidenceRepository,
    IPhotoStorage photoStorage,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    PhotoFileValidator fileValidator,
    TimeProvider timeProvider,
    ILogger<PhotoEvidenceService> logger,
    Ocr.IPhotoOcrService? ocrService = null,
    IRegisteredDeviceService? registeredDeviceService = null) : IPhotoEvidenceService
{
    public async Task<UploadPhotoEvidenceResult> UploadAsync(
        Guid readingId,
        UploadPhotoEvidenceRequest request,
        string correlationId,
        CancellationToken cancellationToken,
        string? deviceSessionToken = null)
    {
        ValidateRequest(readingId, request);
        var actorUserId = currentUser.UserId
            ?? throw new UnauthorizedException("O token autenticado não contém o identificador do usuário.");
        var actorEmail = currentUser.Email;
        if (string.IsNullOrWhiteSpace(actorEmail))
        {
            throw new UnauthorizedException("O token autenticado não contém o e-mail do usuário.");
        }

        if (request.ClientEventId.HasValue)
        {
            var existingByEvent = await photoEvidenceRepository.GetByClientEventIdAsync(
                request.ClientEventId.Value,
                cancellationToken);
            if (existingByEvent is not null)
            {
                if (existingByEvent.ReadingId != readingId)
                {
                    throw new ConflictException(
                        "O ClientEventId já pertence à evidência de outra leitura.");
                }

                return new UploadPhotoEvidenceResult(ToResponse(existingByEvent), true);
            }
        }

        var reading = await readingRepository.GetByIdAsync(readingId, cancellationToken)
            ?? throw new EntityNotFoundException("Leitura", readingId);

        if (reading.RegisteredDeviceId.HasValue)
        {
            if (registeredDeviceService is null)
            {
                throw new ForbiddenException("Comprove um dispositivo autorizado antes de enviar a evidência fotográfica.");
            }

            var sessionDeviceId = await registeredDeviceService.ValidateDeviceSessionAsync(deviceSessionToken, cancellationToken);
            if (sessionDeviceId != reading.RegisteredDeviceId.Value)
            {
                throw new ForbiddenException("A evidência fotográfica deve ser enviada pelo mesmo dispositivo que registrou a leitura.");
            }
        }

        // LEGACY COMPATIBILITY EXCEPTION: readings created before device identity keep
        // RegisteredDeviceId null; no device relationship is invented or backfilled.
        await using var file = await fileValidator.ValidateAndSpoolAsync(
            request.Content,
            request.ContentType,
            cancellationToken);

        var existing = await photoEvidenceRepository.GetByReadingIdAsync(
            readingId,
            cancellationToken);
        if (existing is not null)
        {
            if (string.Equals(existing.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return new UploadPhotoEvidenceResult(ToResponse(existing), true);
            }

            throw new ConflictException(
                "A leitura já possui uma evidência fotográfica e ela não pode ser substituída.");
        }

        var photoEvidenceId = Guid.NewGuid();
        var objectKey = PhotoFileValidator.CreateStorageObjectKey(
            readingId,
            photoEvidenceId,
            file.Extension);
        var originalFileName = PhotoFileValidator.SanitizeOriginalFileName(
            request.OriginalFileName,
            file.Extension);

        await photoStorage.UploadAsync(
            objectKey,
            file.Content,
            file.ContentType,
            cancellationToken);

        var now = timeProvider.GetUtcNow();
        var photoEvidence = new Domain.Entities.PhotoEvidence(
            photoEvidenceId,
            readingId,
            photoStorage.ProviderName,
            photoStorage.ContainerName,
            objectKey,
            originalFileName,
            file.ContentType,
            file.FileSizeBytes,
            file.Sha256,
            request.CapturedAtDevice,
            now,
            file.Width,
            file.Height,
            actorUserId,
            actorEmail,
            NormalizeDeviceId(request.DeviceId),
            request.ClientEventId);
        photoEvidenceRepository.Add(photoEvidence);
        auditLogRepository.Add(new AuditLog(
            AuditActions.PhotoEvidenceCreated,
            nameof(Domain.Entities.PhotoEvidence),
            photoEvidence.Id.ToString(),
            null,
            JsonSerializer.Serialize(
                new
                {
                    PhotoEvidenceId = photoEvidence.Id,
                    photoEvidence.ReadingId,
                    photoEvidence.Sha256,
                },
                ApplicationJson.Options),
            now,
            correlationId,
            actorUserId,
            actorEmail));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            await TryDeleteUploadedBlobAsync(objectKey);
            unitOfWork.ClearChanges();

            var racedEvidence = request.ClientEventId.HasValue
                ? await photoEvidenceRepository.GetByClientEventIdAsync(
                    request.ClientEventId.Value,
                    cancellationToken)
                : null;
            racedEvidence ??= await photoEvidenceRepository.GetByReadingIdAsync(
                readingId,
                cancellationToken);

            if (racedEvidence is not null
                && racedEvidence.ReadingId == readingId
                && (string.Equals(
                        racedEvidence.Sha256,
                        file.Sha256,
                        StringComparison.OrdinalIgnoreCase)
                    || (request.ClientEventId.HasValue
                        && racedEvidence.ClientEventId == request.ClientEventId)))
            {
                return new UploadPhotoEvidenceResult(ToResponse(racedEvidence), true);
            }

            throw new ConflictException(
                "A leitura já possui uma evidência fotográfica ou o ClientEventId já foi utilizado.");
        }
        catch
        {
            await TryDeleteUploadedBlobAsync(objectKey);
            throw;
        }

        if (ocrService is not null)
        {
            try
            {
                await ocrService.ProcessAsync(readingId, false, correlationId, CancellationToken.None);
            }
            catch (Exception)
            {
                // Storage and metadata are already committed. OCR persistence outages must not roll them back.
                logger.LogError("OCR could not finalize after photo commit. ReadingId={ReadingId} PhotoEvidenceId={PhotoEvidenceId}", readingId, photoEvidence.Id);
            }
        }
        return new UploadPhotoEvidenceResult(ToResponse(photoEvidence), false);
    }

    public async Task<PhotoEvidenceResponse> GetMetadataAsync(
        Guid readingId,
        CancellationToken cancellationToken)
    {
        var evidence = await photoEvidenceRepository.GetByReadingIdAsync(
            readingId,
            cancellationToken)
            ?? throw new EntityNotFoundException("Evidência fotográfica", readingId);
        return ToResponse(evidence);
    }

    public async Task<PhotoEvidenceContent> OpenContentAsync(
        Guid readingId,
        CancellationToken cancellationToken)
    {
        var evidence = await photoEvidenceRepository.GetByReadingIdAsync(
            readingId,
            cancellationToken)
            ?? throw new EntityNotFoundException("Evidência fotográfica", readingId);
        var storedContent = await photoStorage.OpenReadAsync(
            evidence.StorageObjectKey,
            cancellationToken);
        return new PhotoEvidenceContent(
            storedContent.Content,
            evidence.ContentType,
            storedContent.ContentLength,
            evidence.OriginalFileName);
    }

    private static void ValidateRequest(Guid readingId, UploadPhotoEvidenceRequest request)
    {
        if (readingId == Guid.Empty)
        {
            throw new ApplicationValidationException(nameof(readingId), "A leitura é obrigatória.");
        }

        if (request.ClientEventId == Guid.Empty)
        {
            throw new ApplicationValidationException(
                nameof(request.ClientEventId),
                "O ClientEventId, quando informado, deve ser um UUID válido.");
        }

        if (request.DeviceId?.Trim().Length > 100)
        {
            throw new ApplicationValidationException(
                nameof(request.DeviceId),
                "O DeviceId deve possuir no máximo 100 caracteres.");
        }
    }

    private static string? NormalizeDeviceId(string? deviceId) =>
        string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();

    private async Task TryDeleteUploadedBlobAsync(string objectKey)
    {
        try
        {
            await photoStorage.DeleteIfExistsAsync(objectKey, CancellationToken.None);
        }
        catch (Exception cleanupException)
        {
            logger.LogError(
                cleanupException,
                "Falha ao compensar upload de evidência no storage. ObjectKey={ObjectKey}",
                objectKey);
        }
    }

    private static PhotoEvidenceResponse ToResponse(Domain.Entities.PhotoEvidence evidence) =>
        new(
            evidence.Id,
            evidence.ReadingId,
            evidence.OriginalFileName,
            evidence.ContentType,
            evidence.FileSizeBytes,
            evidence.Sha256,
            evidence.CapturedAtDevice,
            evidence.UploadedAtServer,
            evidence.Width,
            evidence.Height,
            evidence.Status,
            evidence.CreatedByUserId,
            evidence.CreatedByEmail,
            evidence.CreatedAt,
            evidence.DeviceId,
            evidence.ClientEventId);
}
