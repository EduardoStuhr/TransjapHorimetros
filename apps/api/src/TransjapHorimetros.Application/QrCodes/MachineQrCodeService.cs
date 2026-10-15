using System.Security.Cryptography;
using System.Text.Json;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Application.QrCodes;

public sealed class MachineQrCodeService(
    IMachineRepository machineRepository,
    IMachineQrCodeRepository machineQrCodeRepository,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IMachineQrCodeService
{
    private const string TokenUniqueIndex = "ux_machine_qr_codes_token";
    private const string ActiveMachineUniqueIndex = "ux_machine_qr_codes_machine_active";

    public async Task<QrResolutionResponse> ResolveAsync(
        string value,
        CancellationToken cancellationToken)
    {
        var normalizedValue = NormalizeRequiredValue(value, nameof(value));
        var registeredQr = await machineQrCodeRepository.GetActiveByTokenAsync(
            normalizedValue,
            cancellationToken);

        if (registeredQr is not null)
        {
            return ToResolution(registeredQr.Machine, QrResolutionSource.RegisteredQr, registeredQr.Id);
        }

        if (!FleetQrParser.TryParse(normalizedValue, out var fleetNumber))
        {
            throw new EntityNotFoundException("QR code", normalizedValue);
        }

        var machine = await machineRepository.GetByFleetNumberAsync(
            fleetNumber,
            false,
            cancellationToken)
            ?? throw new EntityNotFoundException("Máquina", fleetNumber);

        return ToResolution(machine, QrResolutionSource.LegacyFleetNumber, null);
    }

    public async Task<IReadOnlyList<MachineQrCodeResponse>> GetHistoryAsync(
        Guid machineId,
        CancellationToken cancellationToken)
    {
        await EnsureMachineExistsAsync(machineId, cancellationToken);
        var history = await machineQrCodeRepository.GetHistoryByMachineIdAsync(
            machineId,
            cancellationToken);
        return history.Select(ToResponse).ToArray();
    }

    public async Task<MachineQrCodeResponse> IssueAsync(
        Guid machineId,
        IssueMachineQrCodeRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureMachineExistsAsync(machineId, cancellationToken);

        var token = request.Token is null
            ? GenerateToken()
            : NormalizeRequiredValue(request.Token, nameof(request.Token));
        if (token.Length > MachineQrCode.MaxTokenLength)
        {
            throw new BadRequestException(
                $"O token do QR code deve possuir no máximo {MachineQrCode.MaxTokenLength} caracteres.");
        }

        if (await machineQrCodeRepository.TokenExistsAsync(token, cancellationToken))
        {
            throw new ConflictException("O token informado já pertence ao histórico de um QR code.");
        }

        var now = timeProvider.GetUtcNow();
        var currentQr = await machineQrCodeRepository.GetActiveByMachineIdAsync(
            machineId,
            true,
            cancellationToken);

        if (currentQr is not null)
        {
            var oldValue = Serialize(currentQr);
            currentQr.Deactivate(now);
            auditLogRepository.Add(new AuditLog(
                AuditActions.MachineQrCodeDeactivated,
                nameof(MachineQrCode),
                currentQr.Id.ToString(),
                oldValue,
                Serialize(currentQr),
                now,
                correlationId));
        }

        var newQr = new MachineQrCode(machineId, token, now);
        machineQrCodeRepository.Add(newQr);
        auditLogRepository.Add(new AuditLog(
            AuditActions.MachineQrCodeIssued,
            nameof(MachineQrCode),
            newQr.Id.ToString(),
            null,
            Serialize(newQr),
            now,
            correlationId));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (
            exception.ConstraintName is TokenUniqueIndex or ActiveMachineUniqueIndex)
        {
            throw new ConflictException(
                exception.ConstraintName == TokenUniqueIndex
                    ? "O token informado já pertence ao histórico de um QR code."
                    : "A máquina já possui outro QR code ativo.");
        }

        return ToResponse(newQr);
    }

    public async Task DeactivateActiveAsync(
        Guid machineId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureMachineExistsAsync(machineId, cancellationToken);
        var currentQr = await machineQrCodeRepository.GetActiveByMachineIdAsync(
            machineId,
            true,
            cancellationToken);

        if (currentQr is null)
        {
            return;
        }

        var oldValue = Serialize(currentQr);
        var now = timeProvider.GetUtcNow();
        currentQr.Deactivate(now);
        auditLogRepository.Add(new AuditLog(
            AuditActions.MachineQrCodeDeactivated,
            nameof(MachineQrCode),
            currentQr.Id.ToString(),
            oldValue,
            Serialize(currentQr),
            now,
            correlationId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureMachineExistsAsync(Guid machineId, CancellationToken cancellationToken)
    {
        if (machineId == Guid.Empty)
        {
            throw new BadRequestException("O identificador da máquina é obrigatório.");
        }

        _ = await machineRepository.GetByIdAsync(machineId, false, cancellationToken)
            ?? throw new EntityNotFoundException("Máquina", machineId);
    }

    private static string NormalizeRequiredValue(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadRequestException($"O campo {field} é obrigatório.");
        }

        return value.Trim();
    }

    private static string GenerateToken() =>
        $"TJ-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";

    private static QrResolutionResponse ToResolution(
        Machine machine,
        QrResolutionSource source,
        Guid? qrCodeId) =>
        new(
            machine.Id,
            machine.FleetNumber,
            machine.Model,
            machine.Status,
            machine.MeterUnit,
            source,
            qrCodeId);

    private static MachineQrCodeResponse ToResponse(MachineQrCode qrCode) =>
        new(
            qrCode.Id,
            qrCode.MachineId,
            qrCode.Token,
            qrCode.IsActive,
            qrCode.CreatedAt,
            qrCode.UpdatedAt,
            qrCode.DeactivatedAt);

    private static string Serialize(MachineQrCode qrCode) =>
        JsonSerializer.Serialize(
            new
            {
                qrCode.Id,
                qrCode.MachineId,
                qrCode.Token,
                qrCode.IsActive,
                qrCode.CreatedAt,
                qrCode.UpdatedAt,
                qrCode.DeactivatedAt,
            },
            ApplicationJson.Options);
}
