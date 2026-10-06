using System.Text.Json;
using Microsoft.Extensions.Logging;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Configuration;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Domain.Rules;

namespace TransjapHorimetros.Application.Readings;

public sealed class ReadingService(
    IMachineRepository machineRepository,
    IWorkSiteRepository workSiteRepository,
    IReadingRepository readingRepository,
    IAnomalyRepository anomalyRepository,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork,
    HourMeterReadingPolicy policy,
    HourMeterRulesOptions rulesOptions,
    TimeProvider timeProvider,
    ILogger<ReadingService> logger) : IReadingService
{
    public async Task<PagedResponse<ReadingResponse>> GetPageAsync(
        ReadingQuery query,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        if (query.From.HasValue && query.To.HasValue && query.From > query.To)
        {
            throw new ApplicationValidationException(nameof(query.From), "O início do período não pode ser posterior ao fim.");
        }

        var normalizedQuery = query with
        {
            Page = page,
            PageSize = pageSize,
            From = query.From?.ToUniversalTime(),
            To = query.To?.ToUniversalTime(),
        };
        var result = await readingRepository.GetPageAsync(normalizedQuery, cancellationToken);
        var items = result.Items.Select(reading => ReadingMappings.ToResponse(reading)).ToArray();
        return PagedResponse<ReadingResponse>.Create(items, page, pageSize, result.TotalItems);
    }

    public async Task<ReadingResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var reading = await readingRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("Leitura", id);
        return ReadingMappings.ToResponse(reading);
    }

    public async Task<PagedResponse<ReadingResponse>> GetForMachineAsync(
        Guid machineId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        _ = await machineRepository.GetByIdAsync(machineId, false, cancellationToken)
            ?? throw new EntityNotFoundException("Máquina", machineId);
        var normalized = Paging.Normalize(page, pageSize);
        var result = await readingRepository.GetForMachineAsync(
            machineId,
            normalized.Page,
            normalized.PageSize,
            cancellationToken);
        var items = result.Items.Select(reading => ReadingMappings.ToResponse(reading)).ToArray();
        return PagedResponse<ReadingResponse>.Create(
            items,
            normalized.Page,
            normalized.PageSize,
            result.TotalItems);
    }

    public async Task<CreateReadingResult> CreateAsync(
        CreateReadingRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var machine = await machineRepository.GetByIdAsync(request.MachineId, false, cancellationToken)
            ?? throw new EntityNotFoundException("Máquina", request.MachineId);

        WorkSite? workSite = null;
        if (request.WorkSiteId.HasValue)
        {
            workSite = await workSiteRepository.GetByIdAsync(request.WorkSiteId.Value, false, cancellationToken)
                ?? throw new EntityNotFoundException("Obra", request.WorkSiteId.Value);
        }

        var existing = await readingRepository.GetByClientEventIdAsync(
            request.ClientEventId,
            cancellationToken);
        if (existing is not null)
        {
            return new CreateReadingResult(ReadingMappings.ToResponse(existing, machine, workSite), true);
        }

        var capturedAt = request.CapturedAtDevice.ToUniversalTime();
        var previous = await readingRepository.GetLatestValidBeforeAsync(
            request.MachineId,
            capturedAt,
            cancellationToken);
        var assessment = policy.Assess(
            previous?.Value,
            previous?.CapturedAtDevice,
            request.Value,
            capturedAt,
            rulesOptions.ElapsedTimeToleranceHours);

        var receivedAtServer = timeProvider.GetUtcNow();
        var syncedAt = timeProvider.GetUtcNow();
        var reading = new HourMeterReading(
            request.MachineId,
            request.WorkSiteId,
            request.Value,
            request.ReadingType!.Value,
            assessment.Status,
            capturedAt,
            receivedAtServer,
            syncedAt,
            request.ClientEventId);
        readingRepository.Add(reading);

        if (assessment.AnomalyType.HasValue)
        {
            var anomaly = new Anomaly(
                reading.Id,
                assessment.AnomalyType.Value,
                assessment.Severity ?? AnomalySeverity.Warning,
                assessment.Description ?? "Inconsistência detectada na leitura.",
                syncedAt);
            anomalyRepository.Add(anomaly);
            logger.LogWarning(
                "Leitura suspeita detectada. MachineId={MachineId} ReadingId={ReadingId} AnomalyType={AnomalyType} CorrelationId={CorrelationId}",
                reading.MachineId,
                reading.Id,
                anomaly.Type,
                correlationId);
        }

        auditLogRepository.Add(new AuditLog(
            AuditActions.ReadingCreated,
            nameof(HourMeterReading),
            reading.Id.ToString(),
            null,
            Serialize(reading),
            syncedAt,
            correlationId));

        if (assessment.Status == ReadingStatus.Suspect)
        {
            auditLogRepository.Add(new AuditLog(
                AuditActions.ReadingMarkedSuspect,
                nameof(HourMeterReading),
                reading.Id.ToString(),
                null,
                Serialize(reading),
                syncedAt,
                correlationId));
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (
            exception.ConstraintName == "ux_hour_meter_readings_client_event_id")
        {
            unitOfWork.ClearChanges();
            existing = await readingRepository.GetByClientEventIdAsync(
                request.ClientEventId,
                cancellationToken);
            if (existing is not null)
            {
                return new CreateReadingResult(ReadingMappings.ToResponse(existing, machine, workSite), true);
            }

            throw;
        }

        var persistedReading = await readingRepository.GetByIdAsync(reading.Id, cancellationToken)
            ?? throw new InvalidOperationException("A leitura persistida não pôde ser recarregada.");
        return new CreateReadingResult(ReadingMappings.ToResponse(persistedReading), false);
    }

    private static void Validate(CreateReadingRequest request)
    {
        if (request.MachineId == Guid.Empty)
        {
            throw new ApplicationValidationException(nameof(request.MachineId), "A máquina é obrigatória.");
        }

        if (request.WorkSiteId == Guid.Empty)
        {
            throw new ApplicationValidationException(nameof(request.WorkSiteId), "O identificador da obra é inválido.");
        }

        if (request.Value < 0m || request.Value > 9_999_999_999.99m)
        {
            throw new ApplicationValidationException(nameof(request.Value), "O horímetro deve estar entre 0 e 9999999999,99.");
        }

        if (request.ClientEventId == Guid.Empty)
        {
            throw new ApplicationValidationException(nameof(request.ClientEventId), "O ClientEventId é obrigatório.");
        }

        if (request.CapturedAtDevice == default)
        {
            throw new ApplicationValidationException(nameof(request.CapturedAtDevice), "A data de captura é obrigatória.");
        }

        if (!request.ReadingType.HasValue)
        {
            throw new ApplicationValidationException(nameof(request.ReadingType), "O tipo da leitura é obrigatório.");
        }
    }

    private static string Serialize(HourMeterReading reading) =>
        JsonSerializer.Serialize(
            new
            {
                reading.Id,
                reading.MachineId,
                reading.WorkSiteId,
                reading.Value,
                reading.ReadingType,
                reading.Status,
                reading.CapturedAtDevice,
                reading.ReceivedAtServer,
                reading.SyncedAt,
                reading.ClientEventId,
            },
            ApplicationJson.Options);
}
