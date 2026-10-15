using System.Security.Cryptography;
using System.Text.RegularExpressions;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Application.QrCodes;

public sealed partial class MachineQrCodeService(
    IMachineRepository machineRepository,
    IMachineQrCodeRepository qrCodeRepository,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IMachineQrCodeService
{
    public async Task<QrResolutionResponse> ResolveAsync(
        string value,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(value);

        var registered = await qrCodeRepository.GetActiveByTokenAsync(normalized, cancellationToken);
        if (registered is not null)
        {
            var machine = await machineRepository.GetByIdAsync(
                registered.MachineId,
                false,
                cancellationToken)
                ?? throw new EntityNotFoundException("Máquina", registered.MachineId);

            return ToResolution(machine, "REGISTERED_QR", registered.Id);
        }

        if (TryParseLegacyFleetNumber(normalized, out var fleetNumber))
        {
            var machine = await machineRepository.GetByFleetNumberAsync(
                fleetNumber,
                false,
                cancellationToken)
                ?? throw new EntityNotFoundException("Máquina", fleetNumber);

            return ToResolution(machine, "LEGACY_FLEET_NUMBER", null);
        }

        throw new EntityNotFoundException("QR code", normalized);
    }

    public async Task<IReadOnlyList<MachineQrCodeResponse>> GetHistoryAsync(
        Guid machineId,
        CancellationToken cancellationToken)
    {
        await EnsureMachineExistsAsync(machineId, cancellationToken);
        var items = await qrCodeRepository.GetHistoryByMachineIdAsync(machineId, cancellationToken);
        return items.Select(ToResponse).ToArray();
    }

    public async Task<MachineQrCodeResponse> IssueAsync(
        Guid machineId,
        IssueMachineQrCodeRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureMachineExistsAsync(machineId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var token = string.IsNullOrWhiteSpace(request.Token)
            ? GenerateToken()
            : Normalize(request.Token);

        if (await qrCodeRepository.TokenExistsAsync(token, cancellationToken))
        {
            throw new ConflictException("Este conteúdo de QR code já foi cadastrado e não pode ser reutilizado.");
        }

        var current = await qrCodeRepository.GetActiveByMachineIdAsync(
            machineId,
            true,
            cancellationToken);

        if (current is not null)
        {
            var oldValue = Serialize(current);
            current.Deactivate(now);
            auditLogRepository.Add(new AuditLog(
                AuditActions.MachineQrCodeDeactivated,
                nameof(MachineQrCode),
                current.Id.ToString(),
                oldValue,
                Serialize(current),
                now,
                correlationId));
        }

        var qrCode = new MachineQrCode(machineId, token, now);
        qrCodeRepository.Add(qrCode);
        auditLogRepository.Add(new AuditLog(
            AuditActions.MachineQrCodeIssued,
            nameof(MachineQrCode),
            qrCode.Id.ToString(),
            null,
            Serialize(qrCode),
            now,
            correlationId));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (
            exception.ConstraintName is "ux_machine_qr_codes_token" or "ux_machine_qr_codes_machine_active")
        {
            unitOfWork.ClearChanges();
            throw new ConflictException("Não foi possível emitir o QR code porque o token ou o QR ativo da máquina já existe.");
        }

        return ToResponse(qrCode);
    }

    public async Task DeactivateActiveAsync(
        Guid machineId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureMachineExistsAsync(machineId, cancellationToken);
        var qrCode = await qrCodeRepository.GetActiveByMachineIdAsync(
            machineId,
            true,
            cancellationToken);

        if (qrCode is null)
        {
            return;
        }

        var oldValue = Serialize(qrCode);
        var now = timeProvider.GetUtcNow();
        qrCode.Deactivate(now);
        auditLogRepository.Add(new AuditLog(
            AuditActions.MachineQrCodeDeactivated,
            nameof(MachineQrCode),
            qrCode.Id.ToString(),
            oldValue,
            Serialize(qrCode),
            now,
            correlationId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureMachineExistsAsync(Guid machineId, CancellationToken cancellationToken)
    {
        if (machineId == Guid.Empty)
        {
            throw new ApplicationValidationException(nameof(machineId), "O identificador da máquina é obrigatório.");
        }

        _ = await machineRepository.GetByIdAsync(machineId, false, cancellationToken)
            ?? throw new EntityNotFoundException("Máquina", machineId);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ApplicationValidationException(nameof(value), "O conteúdo do QR code é obrigatório.");
        }

        var normalized = value.Trim();
        if (normalized.Length > 256)
        {
            throw new ApplicationValidationException(nameof(value), "O conteúdo do QR code deve ter no máximo 256 caracteres.");
        }

        return normalized;
    }

    private static bool TryParseLegacyFleetNumber(string value, out int fleetNumber)
    {
        if (int.TryParse(value, out fleetNumber) && fleetNumber > 0)
        {
            return true;
        }

        var match = LegacyFleetRegex().Match(value);
        return match.Success
            && int.TryParse(match.Groups["fleet"].Value, out fleetNumber)
            && fleetNumber > 0;
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return "TJ-" + Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static MachineQrCodeResponse ToResponse(MachineQrCode qrCode) =>
        new(
            qrCode.Id,
            qrCode.MachineId,
            qrCode.Token,
            qrCode.IsActive,
            qrCode.CreatedAt,
            qrCode.DeactivatedAt);

    private static QrResolutionResponse ToResolution(
        Machine machine,
        string source,
        Guid? qrCodeId) =>
        new(
            machine.Id,
            machine.FleetNumber,
            machine.Model,
            machine.Status,
            machine.MeterUnit,
            source,
            qrCodeId);

    private static string Serialize(MachineQrCode qrCode) =>
        System.Text.Json.JsonSerializer.Serialize(
            new
            {
                qrCode.Id,
                qrCode.MachineId,
                qrCode.Token,
                qrCode.IsActive,
                qrCode.CreatedAt,
                qrCode.DeactivatedAt,
            },
            ApplicationJson.Options);

    [GeneratedRegex(@"^FROTA\s*[:#-]?\s*(?<fleet>\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LegacyFleetRegex();
}
