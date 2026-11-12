using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Application.Ocr;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Infrastructure.Persistence.Repositories;

public sealed class PhotoOcrRepository(TransjapDbContext db) : IPhotoOcrRepository
{
    public Task<PhotoOcrResult?> GetLatestAsync(Guid photoEvidenceId, CancellationToken ct) =>
        db.PhotoOcrResults.AsNoTracking().Where(r => r.PhotoEvidenceId == photoEvidenceId)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<PhotoOcrResult>> GetHistoryAsync(Guid photoEvidenceId, CancellationToken ct) =>
        await db.PhotoOcrResults.AsNoTracking().Where(r => r.PhotoEvidenceId == photoEvidenceId)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).ToListAsync(ct);

    public Task<PhotoOcrResult?> GetByRequestIdAsync(Guid requestId, CancellationToken ct) =>
        db.PhotoOcrResults.AsNoTracking().SingleOrDefaultAsync(r => r.RequestId == requestId, ct);

    public Task<bool> HasAnomalyAsync(Guid photoId, AnomalyType type, CancellationToken ct) =>
        db.Anomalies.AnyAsync(a => a.Type == type && db.PhotoOcrResults.Any(r =>
            r.PhotoEvidenceId == photoId && a.OcrResultId == r.Id), ct);

    public Task RecoverExpiredAsync(Guid photoId, OcrOptions options, DateTimeOffset now, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            _ = await db.PhotoEvidences.FromSqlInterpolated(
                $"SELECT * FROM photo_evidence WITH (UPDLOCK, HOLDLOCK) WHERE id = {photoId}")
                .AsNoTracking().SingleAsync(ct);
            await RecoverCoreAsync(photoId, options, now, ct);
            await transaction.CommitAsync(ct);
        });

    private async Task RecoverCoreAsync(Guid photoId, OcrOptions options, DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now.AddSeconds(-options.TimeoutSeconds - 45);
        var abandoned = await db.PhotoOcrResults.Where(r => r.PhotoEvidenceId == photoId
            && r.Status == PhotoOcrStatus.Processing && r.CreatedAt < cutoff).ToListAsync(ct);
        foreach (var item in abandoned)
        {
            item.Complete(PhotoOcrStatus.Failed, null, null, null, null, null, null, null,
                now, (long)(now - item.CreatedAt).TotalMilliseconds, "OCR_INTERRUPTED",
                reviewReason: "OCR_INTERRUPTED");
            db.AuditLogs.Add(new AuditLog(AuditActions.PhotoOcrFailed, nameof(PhotoOcrResult), item.Id.ToString(),
                null, JsonSerializer.Serialize(new { OcrResultId = item.Id, ErrorCode = "OCR_INTERRUPTED" }),
                now, item.CorrelationId));
        }
        if (abandoned.Count > 0) await db.SaveChangesAsync(ct);
    }

    public async Task<PhotoOcrResult?> StartAsync(PhotoOcrResult result, OcrOptions options, CancellationToken ct)
    {
        // A database row lock serializes claims across API instances, including manual cost controls.
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            _ = await db.PhotoEvidences.FromSqlInterpolated(
                $"SELECT * FROM photo_evidence WITH (UPDLOCK, HOLDLOCK) WHERE id = {result.PhotoEvidenceId}")
                .AsNoTracking().SingleAsync(ct);
            // Recover a committed claim if the execution strategy retried after a lost commit acknowledgement.
            var committed = await db.PhotoOcrResults.SingleOrDefaultAsync(r => r.Id == result.Id, ct);
            if (committed is not null)
            {
                await transaction.CommitAsync(ct);
                return committed;
            }
            await RecoverCoreAsync(result.PhotoEvidenceId, options, result.CreatedAt, ct);
            if (result.RequestId.HasValue)
            {
                var replay = await GetByRequestIdAsync(result.RequestId.Value, ct);
                if (replay is not null)
                {
                    if (replay.PhotoEvidenceId != result.PhotoEvidenceId || replay.ActorUserId != result.ActorUserId)
                        throw new ConflictException("Chave de reprocessamento já utilizada.");
                    await transaction.CommitAsync(ct);
                    return null;
                }
            }
            var history = await GetHistoryAsync(result.PhotoEvidenceId, ct);
            if (result.IsInitial && history.Count > 0)
            {
                await transaction.CommitAsync(ct);
                return null;
            }
            if (!result.IsInitial && history.Any(r =>
                r.CreatedAt.AddSeconds(options.ReprocessCooldownSeconds) > result.CreatedAt
                || (r.Status == PhotoOcrStatus.Processing
                    && r.CreatedAt.AddSeconds(options.TimeoutSeconds + 30) > result.CreatedAt)))
                throw new ConflictException("OCR recente ou em processamento. Aguarde o intervalo configurado antes de reprocessar.");
            // SaveChanges may have accepted the tracked insert before a transaction commit failed.
            // If the retry found no committed row, explicitly restore Added for the new transaction.
            db.Entry(result).State = EntityState.Added;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        });
    }
}
