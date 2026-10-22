namespace TransjapHorimetros.Application.RegisteredDevices;

public sealed class DeviceIdentityOptions
{
    public const string SectionName = "DeviceIdentity";
    public bool EnforceForReadings { get; set; } = true;
    public int ChallengeLifetimeSeconds { get; set; } = 120;
    public int SessionLifetimeMinutes { get; set; } = 5;
    public int LastSeenThrottleMinutes { get; set; } = 5;
    public string? SessionSigningKey { get; set; }
}
