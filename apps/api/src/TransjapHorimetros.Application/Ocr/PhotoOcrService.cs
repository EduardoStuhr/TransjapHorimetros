using System.Diagnostics;
using TransjapHorimetros.Application.PhotoEvidence;
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
    ILogger<PhotoOcrService> logger, OcrProcessingGate? processingGate = null) : IPhotoOcrService
{
    private static readonly OcrProcessingGate DefaultGate = new();

    public async Task<PhotoOcrResponse> GetLatestAsync(Guid readingId, CancellationToken ct)
    {
        var reading = await readings.GetByIdAsync(readingId, ct)
            ?? throw new EntityNotFoundException("Leitura", readingId);
        EvidenceAccess.EnsureCanRead(reading, user);
        var photo = await photos.GetByReadingIdAsync(readingId, ct)
            ?? throw new EntityNotFoundException("Evidência fotográfica", readingId);
        var result = await results.GetLatestAsync(photo.Id, ct)
            ?? throw new EntityNotFoundException("Resultado OCR", readingId);
        return ToResponse(result);
    }

    public async Task<IReadOnlyList<PhotoOcrResponse>> GetHistoryAsync(Guid readingId, CancellationToken ct)
    {
        if (!EvidenceAccess.IsSupervisor(user)) throw new ForbiddenException("Histórico restrito à supervisão.");
        var photo = await photos.GetByReadingIdAsync(readingId, ct)
            ?? throw new EntityNotFoundException("Evidência fotográfica", readingId);
        return (await results.GetHistoryAsync(photo.Id, ct)).Select(ToResponse).ToArray();
    }

    public async Task NotifyPhotoPersistedAsync(Guid readingId, string correlationId, CancellationToken ct)
    {
        // Confirmed photo metadata is the durable work source. The worker also survives lost wakeups/restarts.
        if (!options.BackgroundProcessingEnabled) await ProcessAsync(readingId, false, correlationId, ct);
    }

    public Task<PhotoOcrResponse> ProcessAsync(Guid readingId, bool manual, string correlationId, CancellationToken ct)
        => ProcessCoreAsync(readingId, manual, null, correlationId, ct);

    public Task<PhotoOcrResponse> ReprocessAsync(Guid readingId, Guid requestId, string correlationId, CancellationToken ct)
        => ProcessCoreAsync(readingId, true, requestId, correlationId, ct);

    private async Task<PhotoOcrResponse> ProcessCoreAsync(Guid readingId, bool manual, Guid? requestId,
        string correlationId, CancellationToken ct)
    {
        if (manual && !user.Roles.Any(r => r is AppRoles.Supervisor or AppRoles.Administrative or AppRoles.Manager or AppRoles.Admin))
            throw new ForbiddenException("Perfil sem permissão para reprocessar OCR.");
        if (manual && (!user.IsAuthenticated || user.UserId is null || string.IsNullOrWhiteSpace(user.Email)))
            throw new UnauthorizedException("Identidade autenticada obrigatória para reprocessar OCR.");
        var photo = await photos.GetByReadingIdAsync(readingId, ct)
            ?? throw new EntityNotFoundException("Evidência fotográfica", readingId);
        var reading = await readings.GetByIdAsync(readingId, ct)
            ?? throw new EntityNotFoundException("Leitura", readingId);
        if (requestId == Guid.Empty) throw new BadRequestException("Chave de reprocessamento inválida.");
        if (requestId.HasValue)
        {
            var replay = await results.GetByRequestIdAsync(requestId.Value, ct);
            if (replay is not null)
            {
                EnsureSameRequest(replay, photo.Id);
                return ToResponse(replay);
            }
        }
        var result = await results.StartAsync(new PhotoOcrResult(photo.Id, provider.Name, !manual,
            clock.GetUtcNow(), correlationId, manual ? user.UserId : null, requestId), options, ct);
        if (result is null)
        {
            var replay = requestId.HasValue ? await results.GetByRequestIdAsync(requestId.Value, ct) : null;
            if (replay is not null) { EnsureSameRequest(replay, photo.Id); return ToResponse(replay); }
            var latest = await results.GetLatestAsync(photo.Id, ct)
                ?? throw new EntityNotFoundException("Resultado OCR", readingId);
            return ToResponse(latest);
        }

        var timer = Stopwatch.StartNew();
        string? text = null, model = null, errorCode = null;
        var extraction = new MeterExtraction(null, null);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
        var status = PhotoOcrStatus.Failed;
        var gate = processingGate ?? DefaultGate;
        var entered = false;
        try
        {
            await gate.EnterAsync(deadline.Token);
            entered = true;
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
                        : !extraction.Confidence.HasValue || extraction.Confidence < options.MinimumConfidence ? PhotoOcrStatus.LowConfidence
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
        finally { if (entered) gate.Exit(); }

        using var persistence = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var comparison = OcrComparison.Evaluate(reading.Value, extraction, options);
        var previous = reading.PreviousReadingId.HasValue
            ? await readings.GetByIdAsync(reading.PreviousReadingId.Value, persistence.Token) : null;
        var comparable = previous is not null && previous.MeterUnit == reading.MeterUnit;
        var reason = errorCode ?? (status == PhotoOcrStatus.LowConfidence && extraction.Confidence.HasValue
            ? "LOW_CONFIDENCE" : extraction.Reason);
        result.Complete(status, text, model, extraction.Value, extraction.Confidence,
            comparison.AbsoluteDifference, comparison.PercentageDifference, comparison.Matches,
            clock.GetUtcNow(), timer.ElapsedMilliseconds, errorCode, MeterReadingExtractor.Version, reason,
            JsonSerializer.Serialize(extraction.Candidates ?? [], ApplicationJson.Options),
            comparable && extraction.Value.HasValue ? extraction.Value < previous!.Value : null,
            comparable ? previous!.Value : null);
        if (status is PhotoOcrStatus.LowConfidence or PhotoOcrStatus.NoValueDetected)
            await AddAnomaly(AnomalyType.LowOcrConfidence);
        if (comparison.Matches == false) await AddAnomaly(AnomalyType.OcrOperatorDivergence);
        if (status == PhotoOcrStatus.Failed && (await results.GetHistoryAsync(photo.Id, persistence.Token))
            .Count(r => r.Id != result.Id && r.Status == PhotoOcrStatus.Failed) + 1 >= options.FailuresBeforeAlert)
            await AddAnomaly(AnomalyType.LowOcrConfidence);
        var payload = JsonSerializer.Serialize(new { ReadingId = readingId, PhotoEvidenceId = photo.Id,
            OcrResultId = result.Id, result.Provider, result.Status, result.DetectedValue,
            result.Confidence, result.CorrelationId }, ApplicationJson.Options);
        void Audit(string action) => audits.Add(new AuditLog(action, nameof(PhotoOcrResult), result.Id.ToString(),
            null, payload, clock.GetUtcNow(), correlationId, manual ? user.UserId : null, manual ? user.Email : null));
        Audit(status == PhotoOcrStatus.Failed ? AuditActions.PhotoOcrFailed : AuditActions.PhotoOcrProcessed);
        if (manual) Audit(AuditActions.PhotoOcrReprocessed);
        // Once claimed, finalization survives a disconnected HTTP request; it has its own bounded persistence deadline.
        await uow.SaveChangesAsync(persistence.Token);
        logger.LogInformation("OCR ReadingId={ReadingId} PhotoEvidenceId={PhotoEvidenceId} OcrResultId={OcrResultId} Provider={Provider} Status={Status} DurationMs={DurationMs} Confidence={Confidence}",
            readingId, photo.Id, result.Id, result.Provider, result.Status, timer.ElapsedMilliseconds, result.Confidence);
        return ToResponse(result);

        async Task AddAnomaly(AnomalyType type)
        {
            // Includes resolved alerts: a new OCR attempt must not reopen an administrative decision.
            if (!await results.HasAnomalyAsync(photo.Id, type, persistence.Token))
                anomalies.Add(new Anomaly(reading.MachineId, reading.Id, type, AnomalySeverity.Warning,
                    $"OCR auxiliar da evidência {photo.Id}; processamento {result.Id}. Revisão humana necessária.", clock.GetUtcNow(), result.Id));
        }
    }

    private void EnsureSameRequest(PhotoOcrResult result, Guid photoId)
    {
        if (result.PhotoEvidenceId != photoId || result.ActorUserId != user.UserId)
            throw new ConflictException("Chave de reprocessamento já utilizada em outra operação.");
    }

    private PhotoOcrResponse ToResponse(PhotoOcrResult r) => new(r.Id, r.PhotoEvidenceId, r.Status,
        r.DetectedValue, r.Confidence, r.ProcessedAtServer, r.Provider, r.ProviderModel,
        r.MatchesOperatorValue, r.AbsoluteDifference, r.PercentageDifference,
        r.ErrorCode, r.ErrorMessage, r.CreatedAt, r.AlgorithmVersion, r.ReviewReason,
        EvidenceAccess.IsSupervisor(user) && r.CandidatesJson is not null
            ? JsonSerializer.Deserialize<OcrCandidate[]>(r.CandidatesJson, ApplicationJson.Options) : null,
        EvidenceAccess.IsSupervisor(user) ? r.RawText : null,
        r.BelowPreviousReading, r.PreviousReadingValue);
}
