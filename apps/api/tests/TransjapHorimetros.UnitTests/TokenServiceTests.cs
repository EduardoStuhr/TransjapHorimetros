using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using TransjapHorimetros.Infrastructure.Security;

namespace TransjapHorimetros.UnitTests;

public sealed class TokenServiceTests
{
    [Fact]
    public void GenerateAccessToken_WritesOnlyCanonicalRoleClaims()
    {
        var options = Options.Create(new AuthenticationOptions
        {
            Jwt = new JwtOptions
            {
                Issuer = "test-issuer",
                Audience = "test-audience",
                SigningKey = "test-signing-key-with-at-least-32-bytes",
            },
        });
        var service = new TokenService(options, TimeProvider.System);

        var (rawToken, _) = service.GenerateAccessToken(
            Guid.NewGuid(),
            "admin@tests.local",
            "Admin Test",
            ["ADMIN", "SUPERVISOR"]);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(rawToken);
        var roleClaims = token.Claims.Where(claim => claim.Type == "role").ToArray();

        Assert.Equal(["ADMIN", "SUPERVISOR"], roleClaims.Select(claim => claim.Value));
        Assert.DoesNotContain(token.Claims, claim => claim.Type == ClaimTypes.Role);
    }
}
