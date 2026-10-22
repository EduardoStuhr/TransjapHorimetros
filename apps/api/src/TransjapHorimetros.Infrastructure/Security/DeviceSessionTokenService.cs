using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.RegisteredDevices;

namespace TransjapHorimetros.Infrastructure.Security;

public sealed class DeviceSessionTokenService(
    IOptions<AuthenticationOptions> authenticationOptions,
    DeviceIdentityOptions deviceOptions,
    TimeProvider timeProvider) : IDeviceSessionTokenService
{
    private sealed record Payload(Guid DeviceId, Guid UserId, long ExpiresAtUnix, Guid Jti);

    public (string Token, DateTimeOffset ExpiresAt) Create(Guid registeredDeviceId, Guid userId)
    {
        var expiresAt = timeProvider.GetUtcNow().AddMinutes(
            deviceOptions.SessionLifetimeMinutes > 0 ? deviceOptions.SessionLifetimeMinutes : 5);
        var payload = new Payload(registeredDeviceId, userId, expiresAt.ToUnixTimeSeconds(), Guid.NewGuid());
        var encodedPayload = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var signature = Base64Url(Sign(encodedPayload));
        return ($"{encodedPayload}.{signature}", expiresAt);
    }

    public DeviceSessionPrincipal? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return null;
        var parts = token.Split('.');
        if (parts.Length != 2) return null;
        byte[] suppliedSignature;
        try { suppliedSignature = FromBase64Url(parts[1]); }
        catch (FormatException) { return null; }
        var expectedSignature = Sign(parts[0]);
        if (suppliedSignature.Length != expectedSignature.Length
            || !CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature)) return null;
        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(FromBase64Url(parts[0]));
            if (payload is null || payload.DeviceId == Guid.Empty || payload.UserId == Guid.Empty
                || payload.ExpiresAtUnix <= timeProvider.GetUtcNow().ToUnixTimeSeconds()) return null;
            return new DeviceSessionPrincipal(payload.DeviceId, payload.UserId,
                DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAtUnix));
        }
        catch (JsonException) { return null; }
        catch (FormatException) { return null; }
    }

    private byte[] Sign(string encodedPayload)
    {
        var signingKey = !string.IsNullOrWhiteSpace(deviceOptions.SessionSigningKey)
            ? deviceOptions.SessionSigningKey
            : authenticationOptions.Value.Jwt.SigningKey;
        if (string.IsNullOrWhiteSpace(signingKey)) throw new InvalidOperationException("Chave de sessao nao configurada.");
        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.ASCII.GetBytes(encodedPayload));
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] FromBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized += new string('=', (4 - normalized.Length % 4) % 4);
        return Convert.FromBase64String(normalized);
    }
}
