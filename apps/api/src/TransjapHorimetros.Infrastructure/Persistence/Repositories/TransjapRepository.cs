using Microsoft.EntityFrameworkCore;
using Npgsql;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Anomalies;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Application.Machines;
using TransjapHorimetros.Application.Readings;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Infrastructure.Persistence.Repositories;

public sealed class TransjapRepository(TransjapDbContext dbContext) :
    IMachineRepository,
    IWorkSiteRepository,
    IReadingRepository,
    IAnomalyRepository,
    IAuditLogRepository,
    IUnitOfWork
{
    Task<Machine?> IMachineRepository.GetByIdAsync(
        Guid id,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = trackChanges ? dbContext.Machines : dbContext.Machines.AsNoTracking();
        return query.SingleOrDefaultAsync(machine => machine.Id == id, cancellationToken);
    }

    public Task<Machine?> GetByFleetNumberAsync(
        int fleetNumber,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = trackChanges ? dbContext.Machines : dbContext.Machines.AsNoTracking();
        return query.SingleOrDefaultAsync(
            machine => machine.FleetNumber == fleetNumber,
            cancellationToken);
    }

    public Task<bool> FleetNumberExistsAsync(
        int fleetNumber,
        Guid? excludingId,
        CancellationToken cancellationToken) =>
        dbContext.Machines.AnyAsync(
            machine => machine.FleetNumber == fleetNumber
                && (!excludingId.HasValue || machine.Id != excludingId.Value),
            cancellationToken);

    public async Task<RepositoryPage<Machine>> GetPageAsync(
        MachineQuery query,
        CancellationToken cancellationToken)
    {
        var machines = dbContext.Machines.AsNoTracking().AsQueryable();

        if (query.FleetNumber.HasValue)
        {
            machines = machines.Where(machine => machine.FleetNumber == query.FleetNumber.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Model))
        {
            var model = $"%{query.Model.Trim()}%";
            machines = machines.Where(machine => EF.Functions.ILike(machine.Model, model));
        }

        if (query.Status.HasValue)
        {
            machines = machines.Where(machine => machine.Status == query.Status.Value);
        }

        var total = await machines.CountAsync(cancellationToken);
        var items = await machines
            .OrderBy(machine => machine.FleetNumber)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new RepositoryPage<Machine>(items, total);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken) =>
        dbContext.Machines.CountAsync(cancellationToken);

    public void Add(Machine machine) => dbContext.Machines.Add(machine);

    Task<WorkSite?> IWorkSiteRepository.GetByIdAsync(
        Guid id,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = trackChanges ? dbContext.WorkSites : dbContext.WorkSites.AsNoTracking();
        return query.SingleOrDefaultAsync(workSite => workSite.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkSite>> GetAllAsync(
        bool? active,
        CancellationToken cancellationToken)
    {
        var workSites = dbContext.WorkSites.AsNoTracking().AsQueryable();
        if (active.HasValue)
        {
            workSites = workSites.Where(workSite => workSite.Active == active.Value);
        }

        return await workSites
            .OrderBy(workSite => workSite.Name)
            .ThenBy(workSite => workSite.Code)
            .ToListAsync(cancellationToken);
    }

    public void Add(WorkSite workSite) => dbContext.WorkSites.Add(workSite);

    Task<HourMeterReading?> IReadingRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ReadingsWithReferences()
            .SingleOrDefaultAsync(reading => reading.Id == id, cancellationToken);

    public Task<HourMeterReading?> GetByClientEventIdAsync(
        Guid clientEventId,
        CancellationToken cancellationToken) =>
        ReadingsWithReferences()
            .SingleOrDefaultAsync(
                reading => reading.ClientEventId == clientEventId,
                cancellationToken);

    public Task<HourMeterReading?> GetLatestValidBeforeAsync(
        Guid machineId,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken) =>
        dbContext.HourMeterReadings
            .AsNoTracking()
            .Where(reading => reading.MachineId == machineId)
            .Where(reading => reading.CapturedAtDevice <= capturedAt)
            .Where(reading =>
                reading.Status == ReadingStatus.Validated
                || reading.Status == ReadingStatus.Corrected)
            .OrderByDescending(reading => reading.CapturedAtDevice)
            .ThenByDescending(reading => reading.ReceivedAtServer)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<HourMeterReading>> GetLatestByMachineIdsAsync(
        IReadOnlyCollection<Guid> machineIds,
        CancellationToken cancellationToken)
    {
        if (machineIds.Count == 0)
        {
            return [];
        }

        var latestReadingIds = await dbContext.HourMeterReadings
            .AsNoTracking()
            .Where(reading => machineIds.Contains(reading.MachineId))
            .GroupBy(reading => reading.MachineId)
            .Select(group => group
                .OrderByDescending(reading => reading.CapturedAtDevice)
                .ThenByDescending(reading => reading.ReceivedAtServer)
                .Select(reading => reading.Id)
                .First())
            .ToListAsync(cancellationToken);

        return await ReadingsWithReferences()
            .Where(reading => latestReadingIds.Contains(reading.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<RepositoryPage<HourMeterReading>> GetPageAsync(
        ReadingQuery query,
        CancellationToken cancellationToken)
    {
        var readings = ReadingsWithReferences();

        if (query.MachineId.HasValue)
        {
            readings = readings.Where(reading => reading.MachineId == query.MachineId.Value);
        }

        if (query.FleetNumber.HasValue)
        {
            readings = readings.Where(reading => reading.Machine!.FleetNumber == query.FleetNumber.Value);
        }

        if (query.WorkSiteId.HasValue)
        {
            readings = readings.Where(reading => reading.WorkSiteId == query.WorkSiteId.Value);
        }

        if (query.Status.HasValue)
        {
            readings = readings.Where(reading => reading.Status == query.Status.Value);
        }

        if (query.From.HasValue)
        {
            readings = readings.Where(reading => reading.CapturedAtDevice >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            readings = readings.Where(reading => reading.CapturedAtDevice <= query.To.Value);
        }

        return await PageReadingsAsync(readings, query.Page, query.PageSize, cancellationToken);
    }

    public Task<RepositoryPage<HourMeterReading>> GetForMachineAsync(
        Guid machineId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        PageReadingsAsync(
            ReadingsWithReferences().Where(reading => reading.MachineId == machineId),
            page,
            pageSize,
            cancellationToken);

    public Task<int> CountByStatusAsync(ReadingStatus status, CancellationToken cancellationToken) =>
        dbContext.HourMeterReadings.CountAsync(
            reading => reading.Status == status,
            cancellationToken);

    public Task<int> CountDistinctMachinesReceivedSinceAsync(
        DateTimeOffset receivedSince,
        CancellationToken cancellationToken) =>
        dbContext.HourMeterReadings
            .Where(reading => reading.ReceivedAtServer >= receivedSince)
            .Select(reading => reading.MachineId)
            .Distinct()
            .CountAsync(cancellationToken);

    public void Add(HourMeterReading reading) => dbContext.HourMeterReadings.Add(reading);

    Task<Anomaly?> IAnomalyRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        AnomaliesWithReferences()
            .SingleOrDefaultAsync(anomaly => anomaly.Id == id, cancellationToken);

    public async Task<RepositoryPage<Anomaly>> GetPageAsync(
        AnomalyQuery query,
        CancellationToken cancellationToken)
    {
        var anomalies = AnomaliesWithReferences();

        if (query.MachineId.HasValue)
        {
            anomalies = anomalies.Where(
                anomaly => anomaly.Reading.MachineId == query.MachineId.Value);
        }

        if (query.Type.HasValue)
        {
            anomalies = anomalies.Where(anomaly => anomaly.Type == query.Type.Value);
        }

        if (query.Status.HasValue)
        {
            anomalies = anomalies.Where(anomaly => anomaly.Status == query.Status.Value);
        }

        var total = await anomalies.CountAsync(cancellationToken);
        var items = await anomalies
            .OrderByDescending(anomaly => anomaly.CreatedAt)
            .ThenByDescending(anomaly => anomaly.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new RepositoryPage<Anomaly>(items, total);
    }

    public Task<int> CountOpenAsync(Guid? machineId, CancellationToken cancellationToken)
    {
        var anomalies = dbContext.Anomalies.Where(anomaly => anomaly.Status == AnomalyStatus.Open);
        if (machineId.HasValue)
        {
            anomalies = anomalies.Where(anomaly => anomaly.Reading.MachineId == machineId.Value);
        }

        return anomalies.CountAsync(cancellationToken);
    }

    public void Add(Anomaly anomaly) => dbContext.Anomalies.Add(anomaly);

    public void Add(AuditLog auditLog) => dbContext.AuditLogs.Add(auditLog);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            } postgresException)
        {
            throw new UniqueConstraintException(postgresException.ConstraintName, exception);
        }
    }

    public void ClearChanges() => dbContext.ChangeTracker.Clear();

    private IQueryable<HourMeterReading> ReadingsWithReferences() =>
        dbContext.HourMeterReadings
            .AsNoTracking()
            .Include(reading => reading.Machine)
            .Include(reading => reading.WorkSite);

    private IQueryable<Anomaly> AnomaliesWithReferences() =>
        dbContext.Anomalies
            .AsNoTracking()
            .Include(anomaly => anomaly.Reading)
                .ThenInclude(reading => reading.Machine);

    private static async Task<RepositoryPage<HourMeterReading>> PageReadingsAsync(
        IQueryable<HourMeterReading> readings,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var total = await readings.CountAsync(cancellationToken);
        var items = await readings
            .OrderByDescending(reading => reading.CapturedAtDevice)
            .ThenByDescending(reading => reading.ReceivedAtServer)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new RepositoryPage<HourMeterReading>(items, total);
    }
}
