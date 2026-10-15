using TransjapHorimetros.Domain.Common;

namespace TransjapHorimetros.Domain.Entities;

public sealed class MachineQrCode : AuditableEntity
{
    private MachineQrCode()
    {
    }

    public MachineQrCode(Guid machineId, string token, DateTimeOffset now)
    {
        if (machineId == Guid.Empty)
        {
            throw new ArgumentException("MachineId é obrigatório.", nameof(machineId));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token do QR code é obrigatório.", nameof(token));
        }

        Id = Guid.NewGuid();
        MachineId = machineId;
        Token = token.Trim();
        IsActive = true;
        CreatedAt = now.ToUniversalTime();
        UpdatedAt = CreatedAt;
    }

    public Guid MachineId { get; private set; }

    public Machine Machine { get; private set; } = null!;

    public string Token { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        DeactivatedAt = now.ToUniversalTime();
        UpdatedAt = DeactivatedAt.Value;
    }
}
