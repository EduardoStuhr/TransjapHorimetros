using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TransjapHorimetros.Application.Ocr;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Infrastructure.Persistence;

namespace TransjapHorimetros.Infrastructure.Ocr;

// Photo metadata and the existing initial-result constraint form the durable work source.
public sealed class PhotoOcrWorker(IServiceScopeFactory scopes, OcrOptions options,
    ILogger<PhotoOcrWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), stoppingToken);
                await RunBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception)
            {
                logger.LogError("OCR background batch failed; confirmed photos remain available for the next scan.");
            }
        }
    }

    private async Task RunBatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-options.TimeoutSeconds - 45);
        var stale = await db.PhotoOcrResults.AsNoTracking().Where(r => r.Status == PhotoOcrStatus.Processing
            && r.CreatedAt < cutoff).Select(r => r.PhotoEvidenceId).Distinct().Take(10).ToArrayAsync(ct);
        var repository = scope.ServiceProvider.GetRequiredService<IPhotoOcrRepository>();
        foreach (var photo in stale) await repository.RecoverExpiredAsync(photo, options, DateTimeOffset.UtcNow, ct);
        var pending = await db.PhotoEvidences.AsNoTracking()
            .Where(p => !db.PhotoOcrResults.Any(r => r.PhotoEvidenceId == p.Id))
            .OrderBy(p => p.UploadedAtServer).Select(p => p.ReadingId)
            .Take(options.MaximumConcurrentProcessing).ToArrayAsync(ct);
        await Task.WhenAll(pending.Select(async readingId =>
        {
            using var work = scopes.CreateScope();
            try
            {
                await work.ServiceProvider.GetRequiredService<IPhotoOcrService>()
                    .ProcessAsync(readingId, false, "ocr-worker-" + Guid.NewGuid().ToString("N"), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception)
            {
                logger.LogWarning("OCR job interrupted. ReadingId={ReadingId}", readingId);
            }
        }));
    }
}
