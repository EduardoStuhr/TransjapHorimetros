using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TransjapHorimetros.Application.Abstractions;

namespace TransjapHorimetros.Infrastructure.Security;

public sealed class TokenService(
    IOptions<AuthenticationOptions> options,
    TimeProvider timeProvider) : ITokenService
{
    private readonly AuthenticationOptions _options = options.Value;

    public (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(
        Guid userId,
        string email,
        string fullName,
        IEnumerable<string> roles)
    {
        var signingKey = _options.Jwt.SigningKey;
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException("A chave de assinatura JWT não está configurada.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.Jwt.AccessTokenMinutes > 0 ? _options.Jwt.AccessTokenMinutes : 15);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Name, fullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Jwt.Issuer,
            Audience = _options.Jwt.Audience,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials,
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return (handler.WriteToken(token), expiresAt);
    }

    public (string RawToken, string TokenHash, DateTimeOffset ExpiresAt) GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(randomBytes);
        var tokenHash = HashToken(rawToken);
        var days = _options.RefreshTokenDays > 0 ? _options.RefreshTokenDays : 7;
        var expiresAt = timeProvider.GetUtcNow().AddDays(days);

        return (rawToken, tokenHash, expiresAt);
    }

    public string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken.Trim());
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
