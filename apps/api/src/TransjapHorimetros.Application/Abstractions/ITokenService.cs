namespace TransjapHorimetros.Application.Abstractions;

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(
        Guid userId,
        string email,
        string fullName,
        IEnumerable<string> roles);

    (string RawToken, string TokenHash, DateTimeOffset ExpiresAt) GenerateRefreshToken();

    string HashToken(string rawToken);
}
