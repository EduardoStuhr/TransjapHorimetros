using Microsoft.EntityFrameworkCore;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(TransjapDbContext dbContext) : IRefreshTokenRepository
{
    public void Add(RefreshToken token) =>
        dbContext.RefreshTokens.Add(token);

    public Task<RefreshToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken) =>
        dbContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null && token.ExpiresAt > now)
            .ToListAsync(cancellationToken);
    }
}
