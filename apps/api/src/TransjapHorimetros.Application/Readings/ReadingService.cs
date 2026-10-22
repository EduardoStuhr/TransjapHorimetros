using System.Text.Json;
using Microsoft.Extensions.Logging;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Application.RegisteredDevices;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Domain.Rules;
using TransjapHorimetros.Domain.Security;

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
    ILogger<ReadingService> logger,
    ICurrentUser? currentUser = null,
    IReadingSummaryRepository? summaryRepository = null,
    IRegisteredDeviceService? registeredDeviceService = null) : IReadingService
{
    public async Task<PagedResponse<ReadingResponse>> GetPageAsync(
        ReadingQuery query,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        if ((query.MeterUnit.HasValue && !Enum.IsDefined(query.MeterUnit.Value))
            || (query.ReadingType.HasValue && !Enum.IsDefined(query.ReadingType.Value))
            || (query.Status.HasValue && !Enum.IsDefined(query.Status.Value)))
            throw new ApplicationValidationException("Filters", "Unidade, tipo ou status inválido.");
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
        var summaries = await SummariesAsync(result.Items.Select(r => r.Id).ToArray(), false, cancellationToken);
        var items = result.Items.Select(reading => ReadingMappings.ToResponse(reading, summary: summaries.GetValueOrDefault(reading.Id))).ToArray();
        return PagedResponse<ReadingResponse>.Create(items, page, pageSize, result.TotalItems);
    }

    public async Task<ReadingResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var reading = await readingRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("Leitura", id);
        var summaries = await SummariesAsync([id], true, cancellationToken);
        return ReadingMappings.ToResponse(reading, summary: summaries.GetValueOrDefault(id));
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
        var summaries = await SummariesAsync(result.Items.Select(r => r.Id).ToArray(), false, cancellationToken);
        var items = result.Items.Select(reading => ReadingMappings.ToResponse(reading, summary: summaries.GetValueOrDefault(reading.Id))).ToArray();
        return PagedResponse<ReadingResponse>.Create(
            items,
            normalized.Page,
            normalized.PageSize,
            result.TotalItems);
    }

    public async Task<CreateReadingResult> CreateAsync(
        CreateReadingRequest request,
        string correlationId,
        CancellationToken cancellationToken,
        string? deviceSessionToken = null)
    {
        if (currentUser?.IsAuthenticated != true || currentUser.UserId is null || string.IsNullOrWhiteSpace(currentUser.Email))
            throw new UnauthorizedException("A leitura exige autoria do usuário autenticado.");
        if (request.ClientEventId == Guid.Empty)
            throw new ApplicationValidationException(nameof(request.ClientEventId), "O ClientEventId é obrigatório.");

        // An accepted event is immutable: retries must precede validation of mutable machine/worksite configuration.
        var existing = await readingRepository.GetByClientEventIdAsync(request.ClientEventId, cancellationToken);
        if (existing is not null)
        {
            if (existing.MachineId != request.MachineId)
                throw new ConflictException("O ClientEventId já pertence a outra máquina.");
            return new CreateReadingResult(await GetByIdAsync(existing.Id, cancellationToken), true);
        }
        var registeredDeviceId = registeredDeviceService is null
            ? null
            : await registeredDeviceService.ValidateDeviceSessionAsync(deviceSessionToken, cancellationToken);
        Validate(request);
        var machine = await machineRepository.GetByIdAsync(request.MachineId, false, cancellationToken)
            ?? throw new EntityNotFoundException("Máquina", request.MachineId);
        if (!machine.MeterUnit.HasValue)
            throw new ApplicationValidationException(nameof(machine.MeterUnit), "Defina a unidade HOURS ou KM da máquina antes de registrar leituras.");

        WorkSite? workSite = null;
        if (request.WorkSiteId.HasValue)
        {
            workSite = await workSiteRepository.GetByIdAsync(request.WorkSiteId.Value, false, cancellationToken)
                ?? throw new EntityNotFoundException("Obra", request.WorkSiteId.Value);
            if (!workSite.Active)
                throw new ApplicationValidationException(nameof(request.WorkSiteId), "A obra informada está inativa.");
        }

        // RN-003 — ClientEventId garante idempotência: mesmo evento retorna leitura existente sem criar nova
        HourMeterReading? correctionOf = null;
        if (request.ReadingType == ReadingType.Correction)
        {
            if (!currentUser.Roles.Any(r => r is AppRoles.Supervisor or AppRoles.Administrative or AppRoles.Manager or AppRoles.Admin))
                throw new ForbiddenException("Somente supervisão pode registrar uma proposta de correção.");
            correctionOf = await readingRepository.GetByIdAsync(request.CorrectionOfReadingId!.Value, cancellationToken)
                ?? throw new EntityNotFoundException("Leitura original", request.CorrectionOfReadingId.Value);
        }

        var capturedAt = request.CapturedAtDevice.ToUniversalTime();
        var previous = await readingRepository.GetLatestValidBeforeAsync(
            request.MachineId,
            capturedAt,
            cancellationToken,
            machine.MeterUnit.Value);
        var previousBoundary = await readingRepository.GetLatestBoundaryBeforeAsync(machine.Id,
            machine.MeterUnit.Value, capturedAt, cancellationToken);

        // Detectar possível duplicata: mesmo valor+horário com ClientEventId diferente (RN-005)
        var isPotentialDuplicate = await readingRepository.ExistsSimilarAsync(
            request.MachineId,
            request.Value,
            capturedAt,
            request.ClientEventId,
            cancellationToken);

        // RN-002 — ReceivedAtServer sempre definido pelo servidor; nunca substitui pelo horário do cliente
        var receivedAtServer = timeProvider.GetUtcNow();

        var context = new ReadingAssessmentContext(
            PreviousValue: previous?.Value,
            PreviousCapturedAt: previous?.CapturedAtDevice,
            PreviousReadingType: previous?.ReadingType,
            CurrentValue: request.Value,
            CurrentCapturedAt: capturedAt,
            CurrentReadingType: request.ReadingType!.Value,
            ReceivedAtServer: receivedAtServer,
            IsPotentialDuplicate: isPotentialDuplicate,
            MeterUnit: machine.MeterUnit,
            PreviousBoundaryType: previousBoundary?.ReadingType,
            PreviousBoundaryValue: previousBoundary?.Value,
            PreviousBoundaryCapturedAt: previousBoundary?.CapturedAtDevice);

        var assessment = policy.Assess(context, rulesOptions);

        var syncedAt = receivedAtServer;
        HourMeterReading reading;
        try
        {
            reading = HourMeterReading.CreateOperational(machine, request.WorkSiteId, request.Value,
                request.ReadingType!.Value, assessment.Status, capturedAt, receivedAtServer, request.ClientEventId,
                currentUser.UserId.Value, currentUser.Email, request.DeviceId, registeredDeviceId, previous, correctionOf);
        }
        catch (ArgumentException ex) { throw new ApplicationValidationException("Reading", ex.Message); }
        readingRepository.Add(reading);

        // RN-010 — leitura suspeita permanece auditável; uma Anomaly por detecção
        foreach (var anomalyDetection in assessment.Anomalies)
        {
            var anomaly = new Anomaly(
                reading.MachineId,
                reading.Id,
                anomalyDetection.Type,
                anomalyDetection.Severity,
                anomalyDetection.Description,
                syncedAt);
            anomalyRepository.Add(anomaly);
            logger.LogWarning(
                "Leitura suspeita detectada. MachineId={MachineId} ReadingId={ReadingId} AnomalyType={AnomalyType} CorrelationId={CorrelationId}",
                reading.MachineId,
                reading.Id,
                anomalyDetection.Type,
                correlationId);
        }

        auditLogRepository.Add(new AuditLog(
            AuditActions.ReadingCreated,
            nameof(HourMeterReading),
            reading.Id.ToString(),
            null,
            Serialize(reading),
            syncedAt,
            correlationId,
            currentUser?.UserId,
            currentUser?.Email));

        if (assessment.Status == ReadingStatus.Suspect)
        {
            auditLogRepository.Add(new AuditLog(
                AuditActions.ReadingMarkedSuspect,
                nameof(HourMeterReading),
                reading.Id.ToString(),
                null,
                Serialize(reading),
                syncedAt,
                correlationId,
                currentUser?.UserId,
                currentUser?.Email));
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
                if (existing.MachineId != request.MachineId)
                    throw new ConflictException("O ClientEventId já pertence a outra máquina.");
                return new CreateReadingResult(await GetByIdAsync(existing.Id, cancellationToken), true);
            }

            throw;
        }

        var persistedReading = await readingRepository.GetByIdAsync(reading.Id, cancellationToken)
            ?? throw new InvalidOperationException("A leitura persistida não pôde ser recarregada.");
        var summaries = await SummariesAsync([persistedReading.Id], true, cancellationToken);
        return new CreateReadingResult(ReadingMappings.ToResponse(persistedReading, machine, workSite,
            summaries.GetValueOrDefault(persistedReading.Id)), false);
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

        if (request.Value < 0m || request.Value > 9_999_999_999.99m || decimal.Round(request.Value, 2) != request.Value)
        {
            throw new ApplicationValidationException(nameof(request.Value), "O valor deve estar entre 0 e 9999999999,99 e conter no máximo duas casas decimais.");
        }

        if (request.ClientEventId == Guid.Empty)
        {
            throw new ApplicationValidationException(nameof(request.ClientEventId), "O ClientEventId é obrigatório.");
        }

        if (request.CapturedAtDevice == default)
        {
            throw new ApplicationValidationException(nameof(request.CapturedAtDevice), "A data de captura é obrigatória.");
        }

        if (!request.ReadingType.HasValue || !Enum.IsDefined(request.ReadingType.Value))
        {
            throw new ApplicationValidationException(nameof(request.ReadingType), "Informe um tipo de leitura válido.");
        }
        if ((request.ReadingType == ReadingType.Correction) != request.CorrectionOfReadingId.HasValue
            || request.CorrectionOfReadingId == Guid.Empty)
            throw new ApplicationValidationException(nameof(request.CorrectionOfReadingId), "CORRECTION exige uma leitura original; os demais tipos não aceitam vínculo de correção.");
        try { HourMeterReading.NormalizeDeviceId(request.DeviceId); }
        catch (ArgumentException ex) { throw new ApplicationValidationException(nameof(request.DeviceId), ex.Message); }
    }

    private static string Serialize(HourMeterReading reading) =>
        JsonSerializer.Serialize(
            new
            {
                ReadingId = reading.Id,
                reading.MachineId,
                reading.WorkSiteId,
                reading.Value,
                reading.ReadingType,
                reading.Status,
                reading.CapturedAtDevice,
                reading.ReceivedAtServer,
                reading.SyncedAt,
                reading.ClientEventId,
                reading.MeterUnit,
                reading.CreatedByUserId,
                reading.CreatedByEmail,
                reading.DeviceId,
                reading.PreviousReadingId,
                reading.DifferenceFromPrevious,
                reading.CorrectionOfReadingId,
                reading.RegisteredDeviceId,
            },
            ApplicationJson.Options);

    private Task<IReadOnlyDictionary<Guid, ReadingSummary>> SummariesAsync(Guid[] ids, bool detail, CancellationToken ct) =>
        summaryRepository?.GetAsync(ids, detail, ct)
            ?? Task.FromResult<IReadOnlyDictionary<Guid, ReadingSummary>>(new Dictionary<Guid, ReadingSummary>());
}
