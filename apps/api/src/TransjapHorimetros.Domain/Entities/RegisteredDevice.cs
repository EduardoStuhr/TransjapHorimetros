using TransjapHorimetros.Domain.Common;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Domain.Entities;

public sealed class RegisteredDevice : AuditableEntity
{
    private RegisteredDevice()
    {
    }

    public RegisteredDevice(
        string localDeviceId,
        string displayName,
        string platform,
        string publicKey,
        string publicKeyFingerprint,
        string keyAlgorithm,
        Guid requestedByUserId,
        string requestedByEmail,
        DateTimeOffset requestedAt)
    {
        Id = Guid.NewGuid();
        LocalDeviceId = NormalizeRequired(localDeviceId, 100, nameof(localDeviceId));
        DisplayName = NormalizeRequired(displayName, 100, nameof(displayName));
        Platform = NormalizeRequired(platform, 50, nameof(platform));
        PublicKey = NormalizeRequired(publicKey, 1024, nameof(publicKey));
        PublicKeyFingerprint = NormalizeRequired(publicKeyFingerprint, 64, nameof(publicKeyFingerprint));
        KeyAlgorithm = NormalizeRequired(keyAlgorithm, 50, nameof(keyAlgorithm));
        if (requestedByUserId == Guid.Empty) throw new ArgumentException("O solicitante do dispositivo e obrigatorio.");
        RequestedByUserId = requestedByUserId;
        RequestedByEmail = NormalizeRequired(requestedByEmail, 320, nameof(requestedByEmail));
        RequestedAt = requestedAt.ToUniversalTime();
        Status = RegisteredDeviceStatus.Pending;
        CreatedAt = RequestedAt;
        UpdatedAt = RequestedAt;
    }

    public string LocalDeviceId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Platform { get; private set; } = string.Empty;
    public RegisteredDeviceStatus Status { get; private set; }
    public string PublicKey { get; private set; } = string.Empty;
    public string PublicKeyFingerprint { get; private set; } = string.Empty;
    public string KeyAlgorithm { get; private set; } = string.Empty;
    public Guid RequestedByUserId { get; private set; }
    public string RequestedByEmail { get; private set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; private set; }
    public Guid? AuthorizedByUserId { get; private set; }
    public string? AuthorizedByEmail { get; private set; }
    public DateTimeOffset? AuthorizedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public string? RevokedByEmail { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset? LastSeenAt { get; private set; }
    public ICollection<HourMeterReading> Readings { get; } = new List<HourMeterReading>();
    public ICollection<DeviceChallenge> Challenges { get; } = new List<DeviceChallenge>();

    public void Authorize(Guid actorUserId, string actorEmail, DateTimeOffset now)
    {
        if (Status == RegisteredDeviceStatus.Authorized) return;
        if (Status == RegisteredDeviceStatus.Revoked)
            throw new InvalidOperationException("Um dispositivo revogado nao pode ser reautorizado silenciosamente.");
        AuthorizedByUserId = RequireActor(actorUserId);
        AuthorizedByEmail = NormalizeRequired(actorEmail, 320, nameof(actorEmail));
        AuthorizedAt = now.ToUniversalTime();
        Status = RegisteredDeviceStatus.Authorized;
        UpdatedAt = AuthorizedAt.Value;
    }

    public void Revoke(Guid actorUserId, string actorEmail, DateTimeOffset now)
    {
        if (Status == RegisteredDeviceStatus.Revoked) return;
        RevokedByUserId = RequireActor(actorUserId);
        RevokedByEmail = NormalizeRequired(actorEmail, 320, nameof(actorEmail));
        RevokedAt = now.ToUniversalTime();
        Status = RegisteredDeviceStatus.Revoked;
        UpdatedAt = RevokedAt.Value;
    }

    public void Rename(string displayName, DateTimeOffset now)
    {
        DisplayName = NormalizeRequired(displayName, 100, nameof(displayName));
        UpdatedAt = now.ToUniversalTime();
    }

    public bool MarkSeen(DateTimeOffset now, TimeSpan minimumInterval)
    {
        now = now.ToUniversalTime();
        if (LastSeenAt.HasValue && now - LastSeenAt.Value < minimumInterval) return false;
        LastSeenAt = now;
        UpdatedAt = now;
        return true;
    }

    private static Guid RequireActor(Guid actorUserId) => actorUserId == Guid.Empty
        ? throw new ArgumentException("O responsavel e obrigatorio.")
        : actorUserId;

    private static string NormalizeRequired(string value, int maximumLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{field} e obrigatorio.");
        var normalized = value.Trim();
        if (normalized.Length > maximumLength || normalized.Any(char.IsControl))
            throw new ArgumentException($"{field} e invalido.");
        return normalized;
    }
}
