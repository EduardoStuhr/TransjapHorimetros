namespace TransjapHorimetros.Infrastructure.Security;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    public JwtOptions Jwt { get; set; } = new();

    public int RefreshTokenDays { get; set; } = 7;
}

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "TransjapHorimetros";

    public string Audience { get; set; } = "TransjapHorimetrosApp";

    public int AccessTokenMinutes { get; set; } = 15;

    public string? SigningKey { get; set; }
}
