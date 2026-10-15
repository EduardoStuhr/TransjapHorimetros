namespace TransjapHorimetros.Domain.Entities;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public RefreshToken(
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        string? createdByIp)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("O hash do token de atualização não pode ser vazio.", nameof(tokenHash));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash.Trim();
        CreatedAt = createdAt.ToUniversalTime();
        ExpiresAt = expiresAt.ToUniversalTime();
        CreatedByIp = createdByIp;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public string? CreatedByIp { get; private set; }

    public string? RevokedByIp { get; private set; }

    public bool IsActive(DateTimeOffset utcNow) =>
        RevokedAt is null && utcNow < ExpiresAt;

    public bool IsExpired(DateTimeOffset utcNow) =>
        utcNow >= ExpiresAt;

    public bool IsRevoked => RevokedAt is not null;

    public void Revoke(DateTimeOffset revokedAt, string? revokedByIp, Guid? replacedByTokenId = null)
    {
        RevokedAt = revokedAt.ToUniversalTime();
        RevokedByIp = revokedByIp;
        ReplacedByTokenId = replacedByTokenId;
    }
}
