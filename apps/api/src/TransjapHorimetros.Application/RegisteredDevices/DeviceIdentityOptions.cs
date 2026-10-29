namespace TransjapHorimetros.Application.RegisteredDevices;

public sealed class DeviceIdentityOptions
{
    public const string SectionName = "DeviceIdentity";
    public bool EnforceForReadings { get; set; } = true;
    public int ChallengeLifetimeSeconds { get; set; } = 120;
    public int SessionLifetimeMinutes { get; set; } = 5;
    public int LastSeenThrottleMinutes { get; set; } = 5;
    public string? SessionSigningKey { get; set; }

    public void ValidateForStartup(bool isProduction)
    {
        if (ChallengeLifetimeSeconds is < 1 or > 300)
            throw new InvalidOperationException("DeviceIdentity:ChallengeLifetimeSeconds deve estar entre 1 e 300 segundos.");
        if (SessionLifetimeMinutes is < 1 or > 15)
            throw new InvalidOperationException("DeviceIdentity:SessionLifetimeMinutes deve estar entre 1 e 15 minutos.");
        if (LastSeenThrottleMinutes is < 1 or > 60)
            throw new InvalidOperationException("DeviceIdentity:LastSeenThrottleMinutes deve estar entre 1 e 60 minutos.");

        if (!string.IsNullOrWhiteSpace(SessionSigningKey)
            && System.Text.Encoding.UTF8.GetByteCount(SessionSigningKey) < 32)
        {
            throw new InvalidOperationException(
                "DeviceIdentity:SessionSigningKey deve possuir pelo menos 32 bytes quando configurada.");
        }

        if (isProduction && !EnforceForReadings)
        {
            throw new InvalidOperationException(
                "DeviceIdentity:EnforceForReadings deve ser true em Production.");
        }
    }
}
