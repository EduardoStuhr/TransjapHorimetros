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
