using Microsoft.EntityFrameworkCore;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Readings;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Infrastructure.Persistence.Repositories;

public sealed class ReadingSummaryRepository(TransjapDbContext db) : IReadingSummaryRepository
{
    public async Task<IReadOnlyDictionary<Guid, ReadingSummary>> GetAsync(IReadOnlyCollection<Guid> ids, bool detail, CancellationToken ct)
    {
        if (ids.Count == 0) return new Dictionary<Guid, ReadingSummary>();
        var summaries = await db.HourMeterReadings.AsNoTracking().Where(r => ids.Contains(r.Id))
            .Select(r => new
            {
                r.Id,
                AnomalyCount = r.Anomalies.Count,
                OpenCount = r.Anomalies.Count(a => a.Status == AnomalyStatus.Open),
                CorrectionCount = r.Corrections.Count,
                Ocr = db.PhotoOcrResults.Where(o => r.PhotoEvidence != null && o.PhotoEvidenceId == r.PhotoEvidence.Id)
                    .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
                    .Select(o => new ReadingOcrSummary(o.Status, o.DetectedValue, o.Confidence, o.MatchesOperatorValue)).FirstOrDefault(),
            }).ToListAsync(ct);
        var result = summaries.ToDictionary(r => r.Id, r => new ReadingSummary(r.AnomalyCount, r.OpenCount, r.CorrectionCount, r.Ocr));
        if (!detail) return result;
        var anomalies = await db.Anomalies.AsNoTracking().Where(a => a.ReadingId.HasValue && ids.Contains(a.ReadingId.Value))
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Select(a => new { ReadingId = a.ReadingId!.Value, Summary = new ReadingAnomalySummary(a.Id, a.Type, a.Status, a.Severity, a.Description) }).ToListAsync(ct);
        var corrections = await db.HourMeterReadings.AsNoTracking().Where(r => r.CorrectionOfReadingId.HasValue && ids.Contains(r.CorrectionOfReadingId.Value))
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Select(r => new { ReadingId = r.CorrectionOfReadingId!.Value, Summary = new ReadingCorrectionSummary(r.Id, r.Value, r.Status, r.CreatedAt) }).ToListAsync(ct);
        foreach (var id in result.Keys.ToArray())
            result[id] = result[id] with
            {
                Anomalies = anomalies.Where(a => a.ReadingId == id).Select(a => a.Summary).ToArray(),
                Corrections = corrections.Where(c => c.ReadingId == id).Select(c => c.Summary).ToArray(),
            };
        return result;
    }
}
