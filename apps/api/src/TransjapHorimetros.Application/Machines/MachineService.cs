using System.Text.Json;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Application.Readings;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Application.Machines;

public sealed class MachineService(
    IMachineRepository machineRepository,
    IReadingRepository readingRepository,
    IAnomalyRepository anomalyRepository,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IMachineService
{
    public async Task<PagedResponse<MachineListItemResponse>> GetPageAsync(
        MachineQuery query,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var normalizedQuery = query with { Page = page, PageSize = pageSize };
        var result = await machineRepository.GetPageAsync(normalizedQuery, cancellationToken);
        var machineIds = result.Items.Select(machine => machine.Id).ToArray();
        var latestReadings = await readingRepository.GetLatestByMachineIdsAsync(machineIds, cancellationToken);
        var latestByMachine = latestReadings.ToDictionary(reading => reading.MachineId);

        var items = result.Items
            .Select(machine => ToListItem(machine, latestByMachine.GetValueOrDefault(machine.Id)))
            .ToArray();

        return PagedResponse<MachineListItemResponse>.Create(items, page, pageSize, result.TotalItems);
    }

    public async Task<MachineDetailsResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            throw new ApplicationValidationException(nameof(id), "O identificador da máquina é obrigatório.");
        }

        var machine = await machineRepository.GetByIdAsync(id, false, cancellationToken)
            ?? throw new EntityNotFoundException("Máquina", id);

        return await BuildDetailsAsync(machine, cancellationToken);
    }

    public async Task<MachineDetailsResponse> GetByFleetNumberAsync(
        int fleetNumber,
        CancellationToken cancellationToken)
    {
        if (fleetNumber <= 0)
        {
            throw new ApplicationValidationException(nameof(fleetNumber), "O número de frota deve ser positivo.");
        }

        var machine = await machineRepository.GetByFleetNumberAsync(fleetNumber, false, cancellationToken)
            ?? throw new EntityNotFoundException("Máquina", fleetNumber);

        return await BuildDetailsAsync(machine, cancellationToken);
    }

    public async Task<MachineDetailsResponse> CreateAsync(
        CreateMachineRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        Validate(request.FleetNumber, request.Model);

        if (await machineRepository.FleetNumberExistsAsync(request.FleetNumber, null, cancellationToken))
        {
            throw new ConflictException($"A frota {request.FleetNumber} já está cadastrada.");
        }

        var now = timeProvider.GetUtcNow();
        var machine = new Machine(request.FleetNumber, request.Model, request.Status, now);
        machineRepository.Add(machine);
        auditLogRepository.Add(new AuditLog(
            AuditActions.MachineCreated,
            nameof(Machine),
            machine.Id.ToString(),
            null,
            Serialize(machine),
            now,
            correlationId));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (
            exception.ConstraintName == "ux_machines_fleet_number")
        {
            throw new ConflictException($"A frota {request.FleetNumber} já está cadastrada.");
        }

        return EmptyDetails(machine);
    }

    public async Task<MachineDetailsResponse> UpdateAsync(
        Guid id,
        UpdateMachineRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        Validate(request.FleetNumber, request.Model);
        var machine = await machineRepository.GetByIdAsync(id, true, cancellationToken)
            ?? throw new EntityNotFoundException("Máquina", id);

        if (await machineRepository.FleetNumberExistsAsync(request.FleetNumber, id, cancellationToken))
        {
            throw new ConflictException($"A frota {request.FleetNumber} já está cadastrada.");
        }

        var oldValue = Serialize(machine);
        var now = timeProvider.GetUtcNow();
        machine.Update(request.FleetNumber, request.Model, request.Status, now);
        auditLogRepository.Add(new AuditLog(
            AuditActions.MachineUpdated,
            nameof(Machine),
            machine.Id.ToString(),
            oldValue,
            Serialize(machine),
            now,
            correlationId));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (
            exception.ConstraintName == "ux_machines_fleet_number")
        {
            throw new ConflictException($"A frota {request.FleetNumber} já está cadastrada.");
        }

        return await BuildDetailsAsync(machine, cancellationToken);
    }

    private async Task<MachineDetailsResponse> BuildDetailsAsync(
        Machine machine,
        CancellationToken cancellationToken)
    {
        var readings = await readingRepository.GetForMachineAsync(machine.Id, 1, 10, cancellationToken);
        var responses = readings.Items.Select(reading => ReadingMappings.ToResponse(reading)).ToArray();
        var alertCount = await anomalyRepository.CountOpenAsync(machine.Id, cancellationToken);

        return new MachineDetailsResponse(
            machine.Id,
            machine.FleetNumber,
            machine.Model,
            machine.Status,
            machine.CreatedAt,
            machine.UpdatedAt,
            responses.FirstOrDefault(),
            responses,
            alertCount,
            null);
    }

    private static MachineDetailsResponse EmptyDetails(Machine machine) =>
        new(
            machine.Id,
            machine.FleetNumber,
            machine.Model,
            machine.Status,
            machine.CreatedAt,
            machine.UpdatedAt,
            null,
            [],
            0,
            null);

    private static MachineListItemResponse ToListItem(Machine machine, HourMeterReading? latestReading) =>
        new(
            machine.Id,
            machine.FleetNumber,
            machine.Model,
            machine.Status,
            latestReading is null
                ? null
                : new LatestReadingResponse(
                    latestReading.Id,
                    latestReading.Value,
                    latestReading.CapturedAtDevice,
                    latestReading.WorkSiteId,
                    latestReading.WorkSite?.Name),
            machine.CreatedAt,
            machine.UpdatedAt);

    private static void Validate(int fleetNumber, string model)
    {
        if (fleetNumber <= 0)
        {
            throw new ApplicationValidationException(nameof(fleetNumber), "O número de frota deve ser positivo.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ApplicationValidationException(nameof(model), "O modelo é obrigatório.");
        }
    }

    private static string Serialize(Machine machine) =>
        JsonSerializer.Serialize(
            new { machine.Id, machine.FleetNumber, machine.Model, machine.Status },
            ApplicationJson.Options);
}
