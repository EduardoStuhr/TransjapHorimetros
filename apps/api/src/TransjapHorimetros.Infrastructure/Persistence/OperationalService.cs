using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Dashboard;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Application.Readings;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Infrastructure.Persistence;

public sealed class OperationalService(TransjapDbContext db, ICurrentUser actor, TimeProvider clock) : IOperationalService
{
    public async Task<OperationalSummary> GetSummaryAsync(ReadingQuery q, CancellationToken ct)
    {
        if (q.From > q.To) throw new BadRequestException("O período está invertido.");
        var machines = db.Machines.AsNoTracking().AsQueryable();
        if (q.MachineId.HasValue) machines = machines.Where(m => m.Id == q.MachineId);
        if (q.FleetNumber.HasValue) machines = machines.Where(m => m.FleetNumber == q.FleetNumber);
        var readings = db.HourMeterReadings.AsNoTracking().Where(r => machines.Any(m => m.Id == r.MachineId));
        if (q.WorkSiteId.HasValue) readings = readings.Where(r => r.WorkSiteId == q.WorkSiteId);
        if (q.OperatorId.HasValue) readings = readings.Where(r => r.CreatedByUserId == q.OperatorId);
        if (q.Status.HasValue) readings = readings.Where(r => r.Status == q.Status);
        if (q.From.HasValue) readings = readings.Where(r => r.CapturedAtDevice >= q.From);
        if (q.To.HasValue) readings = readings.Where(r => r.CapturedAtDevice <= q.To);
        if (q.ReadingType.HasValue) readings = readings.Where(r => r.ReadingType == q.ReadingType);
        if (q.HasAnomaly.HasValue) readings = readings.Where(r => r.Anomalies.Any() == q.HasAnomaly);
        var today = new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        var bySite = await readings.GroupBy(r => r.WorkSite == null ? "Sem obra" : r.WorkSite.Name)
            .OrderByDescending(g => g.Count()).Take(100).Select(g => new OperationalGroup(g.Key, g.Count())).ToListAsync(ct);
        var days = await readings.GroupBy(r => r.CapturedAtDevice.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() }).OrderByDescending(g => g.Day).Take(366).ToListAsync(ct);
        var oldest = await machines.Where(m => m.Status == MachineStatus.Active)
            .Select(m => new { m.FleetNumber, m.Model,
                LastReceivedAt = db.HourMeterReadings.Where(r => r.MachineId == m.Id).Max(r => (DateTimeOffset?)r.ReceivedAtServer) })
            .OrderBy(m => m.LastReceivedAt).ThenBy(m => m.FleetNumber).Take(20).ToListAsync(ct);
        return new OperationalSummary(
            await machines.CountAsync(ct), await machines.CountAsync(m => m.Status == MachineStatus.Active, ct),
            await readings.CountAsync(ct), await readings.CountAsync(r => r.ReceivedAtServer >= today, ct),
            await readings.CountAsync(r => (r.Status == ReadingStatus.Pending || r.Status == ReadingStatus.Suspect)
                && !db.ReadingReviews.Any(v => v.ReadingId == r.Id), ct),
            await readings.CountAsync(r => r.Status == ReadingStatus.Suspect, ct),
            await db.Anomalies.CountAsync(a => a.Status == AnomalyStatus.Open && a.ReadingId.HasValue
                && readings.Any(r => r.Id == a.ReadingId), ct),
            bySite, days.Select(d => new OperationalGroup(d.Day.ToString("yyyy-MM-dd"), d.Count)).ToArray(),
            oldest.Select(m => new StaleMachine(m.FleetNumber, m.Model, m.LastReceivedAt)).ToArray());
    }

    public async Task<UtilizationReport> GetUtilizationAsync(ReadingQuery q, CancellationToken ct)
    {
        var to = q.To ?? clock.GetUtcNow();
        var from = q.From ?? to.AddDays(-30);
        if (from > to || to - from > TimeSpan.FromDays(366))
            throw new BadRequestException("Consulte um período de até 366 dias.");
        var machines = db.Machines.AsNoTracking().AsQueryable();
        if (q.MachineId.HasValue) machines = machines.Where(m => m.Id == q.MachineId);
        if (q.FleetNumber.HasValue) machines = machines.Where(m => m.FleetNumber == q.FleetNumber);
        // Preserve chronological neighbours before applying presentation filters.
        var query = db.HourMeterReadings.AsNoTracking()
            .Where(r => machines.Any(m => m.Id == r.MachineId) && r.CapturedAtDevice >= from && r.CapturedAtDevice <= to);
        var points = await query.OrderBy(r => r.MachineId).ThenBy(r => r.CapturedAtDevice).ThenBy(r => r.Id)
            .Select(r => new { r.Id, r.MachineId, r.CapturedAtDevice, r.Value, r.MeterUnit, r.WorkSiteId,
                Site = r.WorkSite == null ? null : r.WorkSite.Name,
                Fleet = r.Machine!.FleetNumber, r.Machine.Model, r.CreatedByUserId, r.Status, r.ReadingType,
                Photo = db.PhotoEvidences.Any(p => p.ReadingId == r.Id),
                Anomaly = r.Anomalies.Any(),
                Corrected = r.CorrectionOfReadingId != null || r.Corrections.Any(),
                Approved = db.ReadingReviews.Where(v => v.ReadingId == r.Id).Select(v => (bool?)v.Approved).FirstOrDefault()
            }).Take(10001).ToListAsync(ct);
        if (points.Count > 10000) throw new BadRequestException("Mais de 10.000 leituras. Reduza o período ou selecione uma frota.");
        var rows = new List<UtilizationRow>();
        foreach (var group in points.GroupBy(p => p.MachineId))
        {
            var intervals = Utilization.Calculate(group.Select(p => new UtilizationPoint(p.Id, p.CapturedAtDevice,
                p.Value, p.MeterUnit, p.WorkSiteId,
                p.Approved == true || (p.Approved == null && p.Status == ReadingStatus.Validated), p.Corrected)), from, to);
            var map = group.ToDictionary(p => p.Id);
            foreach (var interval in intervals)
            {
                var p = map[interval.ReadingId];
                if (q.WorkSiteId.HasValue && p.WorkSiteId != q.WorkSiteId
                    || q.OperatorId.HasValue && p.CreatedByUserId != q.OperatorId
                    || q.Status.HasValue && p.Status != q.Status
                    || q.MeterUnit.HasValue && p.MeterUnit != q.MeterUnit
                    || q.ReadingType.HasValue && p.ReadingType != q.ReadingType
                    || q.HasAnomaly.HasValue && p.Anomaly != q.HasAnomaly
                    || q.HasPhotoEvidence.HasValue && p.Photo != q.HasPhotoEvidence) continue;
                // A site-filtered report counts only intervals attributed to that site.
                var hours = q.WorkSiteId.HasValue && interval.Site != q.WorkSiteId ? null : interval.Difference;
                rows.Add(new(p.MachineId, p.Fleet, p.Model, p.Id, p.CapturedAtDevice,
                    interval.Initial, interval.Final, hours,
                    hours == null && interval.Reason == "CALCULATED" ? "SITE_UNCERTAIN" : interval.Reason,
                    interval.Site, interval.Site == null ? null : p.Site));
            }
        }
        var calculated = rows.Count(r => r.Hours.HasValue);
        return new(calculated == 0 ? null : rows.Sum(r => r.Hours ?? 0),
            calculated, rows.Count - calculated, rows.OrderBy(r => r.FleetNumber).ThenBy(r => r.At).ThenBy(r => r.ReadingId).ToArray());
    }

    public async Task ReviewAnomalyAsync(Guid id, AnomalyReviewRequest request, string correlationId, CancellationToken ct)
    {
        if (actor.UserId is null || string.IsNullOrWhiteSpace(actor.Email))
            throw new ForbiddenException("Usuário autenticado obrigatório.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3 || request.Reason.Length > 1000
            || !Enum.IsDefined(request.Status) || !Enum.IsDefined(request.ExpectedStatus))
            throw new BadRequestException("Informe status e justificativa válidos.");
        var anomaly = await db.Anomalies.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new EntityNotFoundException("Alerta", id);
        if (anomaly.Status != request.ExpectedStatus)
            throw new ConflictException("O alerta foi alterado. Atualize a página.");
        var old = anomaly.Status;
        try { anomaly.Review(request.Status); }
        catch (InvalidOperationException ex) { throw new ConflictException(ex.Message); }
        db.AuditLogs.Add(new AuditLog("ANOMALY_REVIEWED", "Anomaly", id.ToString(), old.ToString(),
            JsonSerializer.Serialize(new { request.Status, Reason = request.Reason.Trim() }), clock.GetUtcNow(),
            correlationId, actor.UserId, actor.Email));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("O alerta foi alterado. Atualize a página."); }
    }

    public Task<ReadingReview?> GetReviewAsync(Guid id, CancellationToken ct) =>
        db.ReadingReviews.AsNoTracking().SingleOrDefaultAsync(r => r.ReadingId == id, ct);

    public async Task<ReadingReview> ReviewAsync(Guid id, ReviewRequest request, string correlationId, CancellationToken ct)
    {
        if (actor.UserId is null || string.IsNullOrWhiteSpace(actor.Email))
            throw new ForbiddenException("Usuário autenticado obrigatório.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3 || request.Reason.Length > 1000)
            throw new BadRequestException("Informe justificativa de 3 a 1000 caracteres.");
        var previous = await GetReviewAsync(id, ct);
        if (previous is not null)
        {
            if (previous.ActorId == actor.UserId && previous.Approved == request.Approved && previous.Reason == request.Reason.Trim())
                return previous;
            throw new ConflictException("Esta leitura já foi revisada. Atualize a página.");
        }
        var reading = await db.HourMeterReadings.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new EntityNotFoundException("Leitura", id);
        if (reading.Status is not (ReadingStatus.Pending or ReadingStatus.Suspect))
            throw new ConflictException("Somente leituras pendentes ou suspeitas podem ser revisadas.");
        var decision = new ReadingReview(id, request.Approved, request.Reason, actor.UserId.Value, actor.Email, clock.GetUtcNow());
        db.ReadingReviews.Add(decision);
        db.AuditLogs.Add(new AuditLog("READING_REVIEWED", "HourMeterReading", id.ToString(), null,
            JsonSerializer.Serialize(new { request.Approved, Reason = request.Reason.Trim() }), clock.GetUtcNow(),
            correlationId, actor.UserId, actor.Email));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            var winner = await GetReviewAsync(id, ct);
            if (winner is not null && winner.ActorId == actor.UserId && winner.Approved == request.Approved
                && winner.Reason == request.Reason.Trim()) return winner;
            throw new ConflictException("Outra decisão foi registrada. Atualize a página.");
        }
        return decision;
    }
}
