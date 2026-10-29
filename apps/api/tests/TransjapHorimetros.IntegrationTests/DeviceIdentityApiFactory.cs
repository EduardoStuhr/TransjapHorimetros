namespace TransjapHorimetros.IntegrationTests;

// Only device expiry tests freeze time. Legacy OCR cooldowns and QR issuance use
// the system clock through TransjapApiFactory, as they do in production.
public sealed class DeviceIdentityApiFactory : TransjapApiFactory
{
    public DeviceIdentityApiFactory() : this(new TestTimeProvider(DateTimeOffset.UtcNow)) { }

    private DeviceIdentityApiFactory(TestTimeProvider clock) : base(clock) => TimeProvider = clock;

    public TestTimeProvider TimeProvider { get; }
}
