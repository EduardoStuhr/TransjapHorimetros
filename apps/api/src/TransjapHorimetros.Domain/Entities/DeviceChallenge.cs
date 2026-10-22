using TransjapHorimetros.Domain.Common;

namespace TransjapHorimetros.Domain.Entities;

public sealed class DeviceChallenge : AuditableEntity
{
    private DeviceChallenge()
    {
    }

    public DeviceChallenge(Guid id, Guid registeredDeviceId, string nonce, string canonicalPayload,
        DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        if (registeredDeviceId == Guid.Empty || string.IsNullOrWhiteSpace(nonce)
            || string.IsNullOrWhiteSpace(canonicalPayload) || expiresAt <= issuedAt)
            throw new ArgumentException("Desafio de dispositivo invalido.");
        if (id == Guid.Empty) throw new ArgumentException("O identificador do desafio e obrigatorio.");
        Id = id;
        RegisteredDeviceId = registeredDeviceId;
        Nonce = nonce;
        CanonicalPayload = canonicalPayload;
        IssuedAt = issuedAt.ToUniversalTime();
        ExpiresAt = expiresAt.ToUniversalTime();
        CreatedAt = IssuedAt;
        UpdatedAt = IssuedAt;
    }

    public Guid RegisteredDeviceId { get; private set; }
    public RegisteredDevice? RegisteredDevice { get; private set; }
    public string Nonce { get; private set; } = string.Empty;
    public string CanonicalPayload { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
}
