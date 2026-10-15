using TransjapHorimetros.Domain.Common;

namespace TransjapHorimetros.Domain.Entities;

public sealed class MachineQrCode : AuditableEntity
{
    public const int MaxTokenLength = 256;

    private MachineQrCode()
    {
    }

    public MachineQrCode(Guid machineId, string token, DateTimeOffset now)
    {
        if (machineId == Guid.Empty)
        {
            throw new ArgumentException("O identificador da máquina é obrigatório.", nameof(machineId));
        }

        var normalizedToken = NormalizeToken(token);
        var utcNow = now.ToUniversalTime();

        Id = Guid.NewGuid();
        MachineId = machineId;
        Token = normalizedToken;
        IsActive = true;
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
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

        var utcNow = now.ToUniversalTime();
        IsActive = false;
        DeactivatedAt = utcNow;
        UpdatedAt = utcNow;
    }

    private static string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("O token do QR code é obrigatório.", nameof(token));
        }

        var normalizedToken = token.Trim();
        if (normalizedToken.Length > MaxTokenLength)
        {
            throw new ArgumentException(
                $"O token do QR code deve possuir no máximo {MaxTokenLength} caracteres.",
                nameof(token));
        }

        return normalizedToken;
    }
}
