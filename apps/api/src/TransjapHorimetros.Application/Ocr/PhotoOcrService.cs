using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Domain.Security;

namespace TransjapHorimetros.Application.Ocr;

public sealed class PhotoOcrService(IPhotoEvidenceRepository photos, IReadingRepository readings,
    IPhotoOcrRepository results, IPhotoStorage storage, IPhotoOcrProvider provider,
    IMeterReadingExtractor extractor, OcrOptions options, IAnomalyRepository anomalies,
    IAuditLogRepository audits, IUnitOfWork uow, ICurrentUser user, TimeProvider clock,
    ILogger<PhotoOcrService> logger) : IPhotoOcrService
{
    public async Task<PhotoOcrResponse> GetLatestAsync(Guid readingId, CancellationToken ct)
    {
        var photo = await photos.GetByReadingIdAsync(readingId, ct)
            ?? throw new EntityNotFoundException("Evidência fotográfica", readingId);
        var result = await results.GetLatestAsync(photo.Id, ct)
            ?? throw new EntityNotFoundException("Resultado OCR", readingId);
        return ToResponse(result);
    }

    public async Task<IReadOnlyList<PhotoOcrResponse>> GetHistoryAsync(Guid readingId, CancellationToken ct)
    {
        var photo = await photos.GetByReadingIdAsync(readingId, ct)
            ?? throw new EntityNotFoundException("Evidência fotográfica", readingId);
        return (await results.GetHistoryAsync(photo.Id, ct)).Select(ToResponse).ToArray();
    }

    public async Task<PhotoOcrResponse> ProcessAsync(Guid readingId, bool manual, string correlationId, CancellationToken ct)
    {
        if (manual && !user.Roles.Any(r => r is AppRoles.Supervisor or AppRoles.Administrative or AppRoles.Manager or AppRoles.Admin))
            throw new ForbiddenException("Perfil sem permissão para reprocessar OCR.");
        var photo = await photos.GetByReadingIdAsync(readingId, ct)
            ?? throw new EntityNotFoundException("Evidência fotográfica", readingId);
        var reading = await readings.GetByIdAsync(readingId, ct)
            ?? throw new EntityNotFoundException("Leitura", readingId);
        var result = await results.StartAsync(new PhotoOcrResult(photo.Id, provider.Name, !manual,
            clock.GetUtcNow(), correlationId, manual ? user.UserId : null), options, ct);
        if (result is null) return await GetLatestAsync(readingId, ct);

        var timer = Stopwatch.StartNew();
        string? text = null, model = null, errorCode = null;
        var extraction = new MeterExtraction(null, null);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
        var status = PhotoOcrStatus.Failed;
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                using var attemptDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                attemptDeadline.CancelAfter(TimeSpan.FromSeconds(Math.Min(5, options.TimeoutSeconds)));
                try
                {
                    var stored = await storage.OpenReadAsync(photo.StorageObjectKey, attemptDeadline.Token)
                        .WaitAsync(attemptDeadline.Token);
                    await using var content = stored.Content;
                    var response = await provider.AnalyzeAsync(content, photo.ContentType, attemptDeadline.Token)
                        .WaitAsync(attemptDeadline.Token);
                    text = OcrText.Limit(response.RawText);
                    model = response.Model;
                    extraction = extractor.Extract(response with { RawText = text }, reading.MeterUnit);
                    status = !extraction.Value.HasValue ? PhotoOcrStatus.NoValueDetected
                        : extraction.Confidence < options.MinimumConfidence ? PhotoOcrStatus.LowConfidence
                        : PhotoOcrStatus.Succeeded;
                    break;
                }
                catch (OcrProviderException ex) when (ex.IsTransient && attempt < options.MaximumRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * (1 << attempt)), deadline.Token);
                }
                catch (OperationCanceledException) when (!deadline.IsCancellationRequested && attempt < options.MaximumRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * (1 << attempt)), deadline.Token);
                }
            }
        }
        catch (OperationCanceledException) { errorCode = "OCR_TIMEOUT"; }
        catch (OcrProviderException ex) { errorCode = ex.Code; }
        catch (Exception) { errorCode = "OCR_PROCESSING_FAILED"; }

        var comparison = OcrComparison.Evaluate(reading.Value, extraction, options);
        result.Complete(status, text, model, extraction.Value, extraction.Confidence,
            comparison.AbsoluteDifference, comparison.PercentageDifference, comparison.Matches,
            clock.GetUtcNow(), timer.ElapsedMilliseconds, errorCode);
        if (comparison.LowConfidence) AddAnomaly(AnomalyType.LowOcrConfidence);
        if (comparison.Matches == false) AddAnomaly(AnomalyType.OcrOperatorDivergence);
        var payload = JsonSerializer.Serialize(new { ReadingId = readingId, PhotoEvidenceId = photo.Id,
            OcrResultId = result.Id, result.Provider, result.Status, result.DetectedValue,
            result.Confidence, result.CorrelationId }, ApplicationJson.Options);
        void Audit(string action) => audits.Add(new AuditLog(action, nameof(PhotoOcrResult), result.Id.ToString(),
            null, payload, clock.GetUtcNow(), correlationId, manual ? user.UserId : null, manual ? user.Email : null));
        Audit(status == PhotoOcrStatus.Failed ? AuditActions.PhotoOcrFailed : AuditActions.PhotoOcrProcessed);
        if (manual) Audit(AuditActions.PhotoOcrReprocessed);
        // Once claimed, finalization survives a disconnected HTTP request; it has its own bounded persistence deadline.
        using var persistence = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await uow.SaveChangesAsync(persistence.Token);
        logger.LogInformation("OCR ReadingId={ReadingId} PhotoEvidenceId={PhotoEvidenceId} OcrResultId={OcrResultId} Provider={Provider} Status={Status} DurationMs={DurationMs} Confidence={Confidence}",
            readingId, photo.Id, result.Id, result.Provider, result.Status, timer.ElapsedMilliseconds, result.Confidence);
        return ToResponse(result);

        void AddAnomaly(AnomalyType type) => anomalies.Add(new Anomaly(reading.MachineId, reading.Id,
            type, AnomalySeverity.Warning, $"OCR auxiliar da evidência {photo.Id}; processamento {result.Id}. Revisão humana necessária.", clock.GetUtcNow(), result.Id));
    }

    private static PhotoOcrResponse ToResponse(PhotoOcrResult r) => new(r.Id, r.PhotoEvidenceId, r.Status,
        r.DetectedValue, r.Confidence, r.ProcessedAtServer, r.Provider, r.ProviderModel,
        r.MatchesOperatorValue, r.AbsoluteDifference, r.PercentageDifference,
        r.ErrorCode, r.ErrorMessage, r.CreatedAt);
}
