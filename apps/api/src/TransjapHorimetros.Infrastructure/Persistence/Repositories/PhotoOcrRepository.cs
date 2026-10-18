using Microsoft.EntityFrameworkCore;
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
            var history = await GetHistoryAsync(result.PhotoEvidenceId, ct);
            if (result.IsInitial && history.Any(r => r.IsInitial))
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
