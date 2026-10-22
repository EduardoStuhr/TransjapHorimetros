using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.RegisteredDevices;

public sealed class RegisteredDeviceService(
    IRegisteredDeviceRepository repository,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork,
    IDeviceSessionTokenService tokenService,
    ICurrentUser currentUser,
    DeviceIdentityOptions options,
    TimeProvider timeProvider) : IRegisteredDeviceService
{
    public const string KeyAlgorithm = "ECDSA_P256_SHA256";

    public async Task<RegisteredDeviceResponse> RegisterAsync(
        RegisterDeviceRequest request, string correlationId, CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        if (!string.Equals(request.KeyAlgorithm, KeyAlgorithm, StringComparison.Ordinal))
            throw new ApplicationValidationException(nameof(request.KeyAlgorithm), "Use ECDSA P-256 com SHA-256.");
        var (normalizedKey, fingerprint) = ValidatePublicKey(request.PublicKey);
        var localDeviceId = Normalize(request.LocalDeviceId, 100, nameof(request.LocalDeviceId));
        var existingByLocalId = await repository.GetByLocalDeviceIdAsync(localDeviceId, false, cancellationToken);
        if (existingByLocalId is not null)
        {
            if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(existingByLocalId.PublicKeyFingerprint), Convert.FromHexString(fingerprint)))
                throw new ConflictException("Este identificador local ja esta vinculado a outra chave.");
            return ToResponse(existingByLocalId);
        }
        var existingByKey = await repository.GetByFingerprintAsync(fingerprint, cancellationToken);
        if (existingByKey is not null)
            throw new ConflictException("Esta chave ja esta vinculada a outro identificador local.");

        var now = timeProvider.GetUtcNow();
        RegisteredDevice device;
        try
        {
            device = new RegisteredDevice(localDeviceId, request.DisplayName, request.Platform,
                normalizedKey, fingerprint, KeyAlgorithm, actor.UserId, actor.Email, now);
        }
        catch (ArgumentException exception)
        {
            throw new ApplicationValidationException("Device", exception.Message);
        }
        repository.Add(device);
        AddAudit(AuditActions.DeviceRegistrationRequested, device, null, Snapshot(device), now, correlationId);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            unitOfWork.ClearChanges();
            var concurrent = await repository.GetByLocalDeviceIdAsync(localDeviceId, false, cancellationToken);
            if (concurrent?.PublicKeyFingerprint == fingerprint) return ToResponse(concurrent);
            throw new ConflictException("O dispositivo foi registrado simultaneamente com dados diferentes.");
        }
        return ToResponse(device);
    }

    public async Task<RegisteredDeviceResponse> GetStatusAsync(string localDeviceId, CancellationToken cancellationToken)
    {
        _ = RequireActor();
        var normalized = Normalize(localDeviceId, 100, nameof(localDeviceId));
        var device = await repository.GetByLocalDeviceIdAsync(normalized, false, cancellationToken)
            ?? throw new EntityNotFoundException("Dispositivo", normalized);
        return ToResponse(device);
    }

    public async Task<PagedResponse<RegisteredDeviceResponse>> GetPageAsync(
        RegisteredDeviceQuery query, CancellationToken cancellationToken)
    {
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
            throw new ApplicationValidationException(nameof(query.Status), "Status de dispositivo invalido.");
        var normalized = Paging.Normalize(query.Page, query.PageSize);
        var result = await repository.GetPageAsync(query with
        {
            Page = normalized.Page,
            PageSize = normalized.PageSize,
            Search = query.Search?.Trim(),
        }, cancellationToken);
        return PagedResponse<RegisteredDeviceResponse>.Create(
            result.Items.Select(ToResponse).ToArray(), normalized.Page, normalized.PageSize, result.TotalItems);
    }

    public async Task<IReadOnlyList<RegisteredDeviceAuditResponse>> GetHistoryAsync(
        Guid id, CancellationToken cancellationToken)
    {
        _ = await repository.GetByIdAsync(id, false, cancellationToken)
            ?? throw new EntityNotFoundException("Dispositivo", id);
        return (await repository.GetHistoryAsync(id, cancellationToken))
            .Select(log => new RegisteredDeviceAuditResponse(log.Action, log.CreatedAt, log.UserId, log.UserEmail))
            .ToArray();
    }

    public Task<RegisteredDeviceResponse> AuthorizeAsync(Guid id, string correlationId, CancellationToken cancellationToken) =>
        ChangeAsync(id, correlationId, cancellationToken, AuditActions.DeviceAuthorized,
            (device, actor, now) => device.Authorize(actor.UserId, actor.Email, now));

    public Task<RegisteredDeviceResponse> RevokeAsync(Guid id, string correlationId, CancellationToken cancellationToken) =>
        ChangeAsync(id, correlationId, cancellationToken, AuditActions.DeviceRevoked,
            (device, actor, now) => device.Revoke(actor.UserId, actor.Email, now));

    public Task<RegisteredDeviceResponse> RenameAsync(
        Guid id, RenameDeviceRequest request, string correlationId, CancellationToken cancellationToken) =>
        ChangeAsync(id, correlationId, cancellationToken, AuditActions.DeviceRenamed,
            (device, _, now) => device.Rename(request.DisplayName, now));

    public async Task<DeviceChallengeResponse> CreateChallengeAsync(
        DeviceChallengeRequest request, CancellationToken cancellationToken)
    {
        _ = RequireActor();
        var localDeviceId = Normalize(request.LocalDeviceId, 100, nameof(request.LocalDeviceId));
        var device = await repository.GetByLocalDeviceIdAsync(localDeviceId, false, cancellationToken)
            ?? throw new EntityNotFoundException("Dispositivo", localDeviceId);
        EnsureAuthorized(device);
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddSeconds(options.ChallengeLifetimeSeconds > 0 ? options.ChallengeLifetimeSeconds : 120);
        var challengeId = Guid.NewGuid();
        var nonce = Base64Url(RandomNumberGenerator.GetBytes(32));
        var payload = string.Join('\n', "TRANSJAP-DEVICE-CHALLENGE", "v1", challengeId.ToString("D"),
            device.Id.ToString("D"), nonce, expiresAt.ToString("O", CultureInfo.InvariantCulture));
        repository.Add(new DeviceChallenge(challengeId, device.Id, nonce, payload, now, expiresAt));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DeviceChallengeResponse(challengeId, payload, expiresAt);
    }

    public async Task<DeviceSessionResponse> VerifyChallengeAsync(
        VerifyDeviceChallengeRequest request, string correlationId, CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        if (request.ChallengeId == Guid.Empty)
            throw new ApplicationValidationException(nameof(request.ChallengeId), "O desafio e obrigatorio.");
        var now = timeProvider.GetUtcNow();
        var challenge = await repository.GetChallengeAsync(request.ChallengeId, cancellationToken)
            ?? throw new EntityNotFoundException("Desafio", request.ChallengeId);
        if (!await repository.TryConsumeChallengeAsync(challenge.Id, now, cancellationToken))
            throw new ConflictException("O desafio expirou ou ja foi utilizado. Solicite um novo desafio.");
        var device = challenge.RegisteredDevice
            ?? throw new EntityNotFoundException("Dispositivo", challenge.RegisteredDeviceId);

        var verified = device.Status == RegisteredDeviceStatus.Authorized
            && VerifySignature(device.PublicKey, challenge.CanonicalPayload, request.Signature);
        if (!verified)
        {
            AddAudit(AuditActions.DeviceChallengeFailed, device, null,
                JsonSerializer.Serialize(new { challengeId = challenge.Id, reason = "invalid_signature_or_status" }),
                now, correlationId);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ForbiddenException(device.Status == RegisteredDeviceStatus.Revoked
                ? "Este dispositivo foi revogado. A fila local foi preservada, mas nao pode ser sincronizada."
                : "Nao foi possivel comprovar a identidade deste dispositivo.");
        }

        var trackedDevice = await repository.GetByIdAsync(device.Id, true, cancellationToken)
            ?? throw new EntityNotFoundException("Dispositivo", device.Id);
        trackedDevice.MarkSeen(now, TimeSpan.FromMinutes(Math.Max(1, options.LastSeenThrottleMinutes)));
        AddAudit(AuditActions.DeviceChallengeVerified, trackedDevice, null,
            JsonSerializer.Serialize(new { challengeId = challenge.Id }), now, correlationId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var session = tokenService.Create(trackedDevice.Id, actor.UserId);
        return new DeviceSessionResponse(session.Token, session.ExpiresAt, ToResponse(trackedDevice));
    }

    public async Task<Guid?> ValidateDeviceSessionAsync(string? token, CancellationToken cancellationToken)
    {
        if (!options.EnforceForReadings) return null;
        var actor = RequireActor();
        var principal = string.IsNullOrWhiteSpace(token) ? null : tokenService.Validate(token);
        if (principal is null || principal.UserId != actor.UserId)
            throw new ForbiddenException("Comprove um dispositivo autorizado antes de enviar a leitura.");
        var device = await repository.GetByIdAsync(principal.RegisteredDeviceId, true, cancellationToken)
            ?? throw new ForbiddenException("O dispositivo comprovado nao existe mais.");
        EnsureAuthorized(device);
        if (device.MarkSeen(timeProvider.GetUtcNow(), TimeSpan.FromMinutes(Math.Max(1, options.LastSeenThrottleMinutes))))
            await unitOfWork.SaveChangesAsync(cancellationToken);
        return device.Id;
    }

    private async Task<RegisteredDeviceResponse> ChangeAsync(
        Guid id, string correlationId, CancellationToken cancellationToken, string action,
        Action<RegisteredDevice, Actor, DateTimeOffset> change)
    {
        var actor = RequireActor();
        var device = await repository.GetByIdAsync(id, true, cancellationToken)
            ?? throw new EntityNotFoundException("Dispositivo", id);
        var oldValue = Snapshot(device);
        try { change(device, actor, timeProvider.GetUtcNow()); }
        catch (ArgumentException exception) { throw new ApplicationValidationException("Device", exception.Message); }
        catch (InvalidOperationException exception) { throw new ConflictException(exception.Message); }
        AddAudit(action, device, oldValue, Snapshot(device), timeProvider.GetUtcNow(), correlationId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(device);
    }

    private Actor RequireActor()
    {
        if (currentUser.IsAuthenticated != true || currentUser.UserId is null || string.IsNullOrWhiteSpace(currentUser.Email))
            throw new UnauthorizedException("A identidade do usuario autenticado e obrigatoria.");
        return new Actor(currentUser.UserId.Value, currentUser.Email);
    }

    private void AddAudit(string action, RegisteredDevice device, string? oldValue, string? newValue,
        DateTimeOffset now, string correlationId)
    {
        var actor = RequireActor();
        auditLogRepository.Add(new AuditLog(action, nameof(RegisteredDevice), device.Id.ToString(), oldValue,
            newValue, now, correlationId, actor.UserId, actor.Email));
    }

    private static void EnsureAuthorized(RegisteredDevice device)
    {
        if (device.Status == RegisteredDeviceStatus.Pending)
            throw new ForbiddenException("Este dispositivo ainda aguarda autorizacao de um responsavel.");
        if (device.Status == RegisteredDeviceStatus.Revoked)
            throw new ForbiddenException("Este dispositivo foi revogado. A fila local foi preservada, mas nao pode ser sincronizada.");
    }

    private static (string PublicKey, string Fingerprint) ValidatePublicKey(string encoded)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(encoded.Trim()); }
        catch (FormatException) { throw new ApplicationValidationException(nameof(encoded), "A chave publica deve estar em SPKI Base64."); }
        if (bytes.Length is < 80 or > 200)
            throw new ApplicationValidationException(nameof(encoded), "A chave publica P-256 possui tamanho invalido.");
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(bytes, out var bytesRead);
            if (bytesRead != bytes.Length || ecdsa.KeySize != 256)
                throw new CryptographicException();
            var normalized = ecdsa.ExportSubjectPublicKeyInfo();
            return (Convert.ToBase64String(normalized), Convert.ToHexString(SHA256.HashData(normalized)).ToLowerInvariant());
        }
        catch (CryptographicException)
        {
            throw new ApplicationValidationException(nameof(encoded), "A chave publica nao e uma chave ECDSA P-256 SPKI valida.");
        }
    }

    private static bool VerifySignature(string publicKey, string payload, string signature)
    {
        try
        {
            var signatureBytes = Convert.FromBase64String(signature);
            if (signatureBytes.Length != 64) return false;
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
            return ecdsa.VerifyData(Encoding.UTF8.GetBytes(payload), signatureBytes, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (FormatException) { return false; }
        catch (CryptographicException) { return false; }
    }

    private static string Normalize(string value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ApplicationValidationException(field, $"{field} e obrigatorio.");
        var normalized = value.Trim();
        if (normalized.Length > maxLength || normalized.Any(char.IsControl))
            throw new ApplicationValidationException(field, $"{field} e invalido.");
        return normalized;
    }

    private static string Snapshot(RegisteredDevice device) => JsonSerializer.Serialize(new
    {
        device.Id,
        device.LocalDeviceId,
        device.DisplayName,
        device.Platform,
        device.Status,
        device.KeyAlgorithm,
        device.PublicKeyFingerprint,
        device.RequestedByUserId,
        device.RequestedByEmail,
        device.RequestedAt,
        device.AuthorizedByUserId,
        device.AuthorizedByEmail,
        device.AuthorizedAt,
        device.RevokedByUserId,
        device.RevokedByEmail,
        device.RevokedAt,
        device.LastSeenAt,
    }, ApplicationJson.Options);

    private static RegisteredDeviceResponse ToResponse(RegisteredDevice device) => new(
        device.Id, device.LocalDeviceId, device.DisplayName, device.Platform, device.Status,
        device.KeyAlgorithm, device.PublicKeyFingerprint, device.RequestedByUserId, device.RequestedByEmail,
        device.RequestedAt, device.AuthorizedByUserId, device.AuthorizedByEmail, device.AuthorizedAt,
        device.RevokedByUserId, device.RevokedByEmail, device.RevokedAt, device.LastSeenAt,
        device.CreatedAt, device.UpdatedAt);

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private sealed record Actor(Guid UserId, string Email);
}
