using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Application.RegisteredDevices;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Infrastructure.Security;

namespace TransjapHorimetros.UnitTests;

public sealed class RegisteredDeviceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Lifecycle_PreservesRequestAndDoesNotReauthorizeRevokedDevice()
    {
        var requester = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var device = new RegisteredDevice("web-local", "Tablet 1", "WEB_PWA", "public-key",
            new string('a', 64), RegisteredDeviceService.KeyAlgorithm, requester, "operator@tests.local", Now);

        device.Authorize(manager, "manager@tests.local", Now.AddMinutes(1));
        device.Revoke(manager, "manager@tests.local", Now.AddMinutes(2));

        Assert.Equal(RegisteredDeviceStatus.Revoked, device.Status);
        Assert.Equal(requester, device.RequestedByUserId);
        Assert.Equal(Now, device.RequestedAt);
        Assert.Throws<InvalidOperationException>(() =>
            device.Authorize(manager, "manager@tests.local", Now.AddMinutes(3)));
    }

    [Fact]
    public void MarkSeen_IsThrottled()
    {
        var device = new RegisteredDevice("web-local", "Tablet 1", "WEB_PWA", "public-key",
            new string('a', 64), RegisteredDeviceService.KeyAlgorithm, Guid.NewGuid(), "operator@tests.local", Now);
        Assert.True(device.MarkSeen(Now.AddMinutes(1), TimeSpan.FromMinutes(5)));
        Assert.False(device.MarkSeen(Now.AddMinutes(2), TimeSpan.FromMinutes(5)));
        Assert.True(device.MarkSeen(Now.AddMinutes(6), TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void DeviceSessionToken_IsBoundToDeviceAndUserAndRejectsTampering()
    {
        var clock = new FixedTimeProvider(Now);
        var service = new DeviceSessionTokenService(Options.Create(new AuthenticationOptions
        {
            Jwt = new JwtOptions { SigningKey = "test-signing-key-with-at-least-32-bytes" },
        }), new DeviceIdentityOptions { SessionLifetimeMinutes = 5 }, clock);
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var session = service.Create(deviceId, userId);

        var principal = service.Validate(session.Token);
        Assert.Equal(deviceId, principal?.RegisteredDeviceId);
        Assert.Equal(userId, principal?.UserId);
        Assert.Null(service.Validate(session.Token + "x"));

        clock.UtcNow = Now.AddMinutes(6);
        Assert.Null(service.Validate(session.Token));
    }

    [Fact]
    public void DeviceSessionToken_UsesDedicatedSigningKeyWhenConfigured()
    {
        var clock = new FixedTimeProvider(Now);
        var jwtKey = "jwt-signing-key-with-at-least-32-bytes";
        var dedicatedKey = "dedicated-device-session-key-32-bytes";

        var dedicatedService = new DeviceSessionTokenService(
            Options.Create(new AuthenticationOptions { Jwt = new JwtOptions { SigningKey = jwtKey } }),
            new DeviceIdentityOptions { SessionLifetimeMinutes = 5, SessionSigningKey = dedicatedKey },
            clock);

        var fallbackService = new DeviceSessionTokenService(
            Options.Create(new AuthenticationOptions { Jwt = new JwtOptions { SigningKey = jwtKey } }),
            new DeviceIdentityOptions { SessionLifetimeMinutes = 5, SessionSigningKey = null },
            clock);

        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var session = dedicatedService.Create(deviceId, userId);

        // Validates successfully with the dedicated key
        var principal = dedicatedService.Validate(session.Token);
        Assert.NotNull(principal);
        Assert.Equal(deviceId, principal.RegisteredDeviceId);
        Assert.Equal(userId, principal.UserId);

        // Fallback service using only the JWT key must reject the token signed with dedicated key
        Assert.Null(fallbackService.Validate(session.Token));
    }

    [Fact]
    public void DeviceSessionToken_RejectsOversizedAndMalformedTokens()
    {
        var clock = new FixedTimeProvider(Now);
        var service = new DeviceSessionTokenService(
            Options.Create(new AuthenticationOptions { Jwt = new JwtOptions { SigningKey = "test-signing-key-with-at-least-32-bytes" } }),
            new DeviceIdentityOptions { SessionLifetimeMinutes = 5 },
            clock);

        Assert.Null(service.Validate(""));
        Assert.Null(service.Validate(new string('a', 2049)));
        Assert.Null(service.Validate("not.a.valid.jwt.token.format"));
        Assert.Null(service.Validate("singlepart"));
    }

    [Fact]
    public void PublicKeyValidation_AcceptsOnlyNistP256()
    {
        using var p256 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var encoded = Convert.ToBase64String(p256.ExportSubjectPublicKeyInfo());

        var validated = RegisteredDeviceService.ValidatePublicKey(encoded);

        Assert.Equal(encoded, validated.PublicKey);
        Assert.Equal(64, validated.Fingerprint.Length);
    }

    [Fact]
    public void PublicKeyValidation_RejectsSecp256k1()
    {
        using var secp256k1 = ECDsa.Create(ECCurve.CreateFromValue("1.3.132.0.10"));
        var encoded = Convert.ToBase64String(secp256k1.ExportSubjectPublicKeyInfo());

        Assert.Throws<ApplicationValidationException>(() =>
            RegisteredDeviceService.ValidatePublicKey(encoded));
    }

    [Fact]
    public void DeviceIdentityOptions_RejectsShortDedicatedSigningKey()
    {
        var options = new DeviceIdentityOptions { SessionSigningKey = "short-key" };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            options.ValidateForStartup(isProduction: false));

        Assert.Contains("pelo menos 32 bytes", exception.Message);
    }

    [Fact]
    public void DeviceIdentityOptions_RequiresEnforcementInProduction()
    {
        var options = new DeviceIdentityOptions { EnforceForReadings = false };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            options.ValidateForStartup(isProduction: true));

        Assert.Contains("deve ser true em Production", exception.Message);
        options.ValidateForStartup(isProduction: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VerifyChallenge_RevokedBeforeOrDuringSave_DoesNotIssueSession(bool revokeDuringSave)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var fingerprint = Convert.ToHexString(SHA256.HashData(key.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
        var userId = Guid.NewGuid();
        var authorized = new RegisteredDevice("web-local", "Tablet 1", "WEB_PWA", publicKey,
            fingerprint, RegisteredDeviceService.KeyAlgorithm, userId, "operator@tests.local", Now);
        authorized.Authorize(Guid.NewGuid(), "manager@tests.local", Now);
        var revoked = new RegisteredDevice("web-local", "Tablet 1", "WEB_PWA", publicKey,
            fingerprint, RegisteredDeviceService.KeyAlgorithm, userId, "operator@tests.local", Now);
        revoked.Authorize(Guid.NewGuid(), "manager@tests.local", Now);
        revoked.Revoke(Guid.NewGuid(), "manager@tests.local", Now.AddSeconds(1));

        var challengeId = Guid.NewGuid();
        const string payload = "TRANSJAP-DEVICE-CHALLENGE\nv1\nrace";
        var challenge = new DeviceChallenge(challengeId, authorized.Id, "nonce", payload, Now, Now.AddMinutes(2));
        typeof(DeviceChallenge).GetProperty(nameof(DeviceChallenge.RegisteredDevice))!
            .SetValue(challenge, authorized);
        var signature = Convert.ToBase64String(key.SignData(
            System.Text.Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var repository = new RaceRepository(challenge, revokeDuringSave ? authorized : revoked);
        var tokenService = new RecordingDeviceSessionTokenService();
        var unitOfWork = new UnitOfWork(revokeDuringSave
            ? () => authorized.Revoke(Guid.NewGuid(), "manager@tests.local", Now.AddSeconds(1))
            : null);
        var service = new RegisteredDeviceService(repository, new AuditRepository(), unitOfWork,
            tokenService, new CurrentUser(userId), new DeviceIdentityOptions(), new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.VerifyChallengeAsync(
            new VerifyDeviceChallengeRequest(challengeId, signature), "correlation", CancellationToken.None));

        Assert.False(tokenService.WasCreated);
    }

    [Theory]
    [InlineData("challenge", 0)]
    [InlineData("challenge", 301)]
    [InlineData("session", 0)]
    [InlineData("session", 16)]
    [InlineData("lastSeen", 0)]
    [InlineData("lastSeen", 61)]
    public void DeviceIdentityOptions_RejectsInvalidSecurityWindows(string setting, int value)
    {
        var options = new DeviceIdentityOptions();
        switch (setting)
        {
            case "challenge": options.ChallengeLifetimeSeconds = value; break;
            case "session": options.SessionLifetimeMinutes = value; break;
            case "lastSeen": options.LastSeenThrottleMinutes = value; break;
        }
        Assert.Throws<InvalidOperationException>(() => options.ValidateForStartup(isProduction: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Register_ConcurrentCommitBetweenLookups_OnlySameIdentityIsIdempotent(bool sameLocalId)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var spki = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var (_, fingerprint) = RegisteredDeviceService.ValidatePublicKey(spki);
        var userId = Guid.NewGuid();
        var existing = new RegisteredDevice("web-local", "Tablet", "WEB_PWA", spki,
            fingerprint, RegisteredDeviceService.KeyAlgorithm, userId, "operator@tests.local", Now);
        var service = new RegisteredDeviceService(new RaceRepository(null, existing), new AuditRepository(),
            new UnitOfWork(), new RecordingDeviceSessionTokenService(), new CurrentUser(userId),
            new DeviceIdentityOptions(), new FixedTimeProvider(Now));
        var request = new RegisterDeviceRequest(sameLocalId ? "web-local" : "web-other", "Tablet", "WEB_PWA",
            spki, RegisteredDeviceService.KeyAlgorithm);

        if (sameLocalId)
        {
            var result = await service.RegisterAsync(request, "race", default);
            Assert.True(result.IsExisting);
            Assert.Equal(existing.Id, result.Device.Id);
        }
        else
        {
            await Assert.ThrowsAsync<ConflictException>(() => service.RegisterAsync(request, "race", default));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class RaceRepository(DeviceChallenge? challenge, RegisteredDevice trackedDevice)
        : IRegisteredDeviceRepository
    {
        public Task<DeviceChallenge?> GetChallengeAsync(Guid challengeId, CancellationToken cancellationToken) =>
            Task.FromResult<DeviceChallenge?>(challenge);
        public Task<bool> TryConsumeChallengeAsync(Guid challengeId, DateTimeOffset consumedAt, CancellationToken cancellationToken) =>
            Task.FromResult(true);
        public Task<RegisteredDevice?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) =>
            Task.FromResult<RegisteredDevice?>(trackedDevice);
        public Task<RegisteredDevice?> GetByLocalDeviceIdAsync(string localDeviceId, bool trackChanges, CancellationToken cancellationToken) =>
            Task.FromResult<RegisteredDevice?>(null);
        public Task<RegisteredDevice?> GetByFingerprintAsync(string fingerprint, CancellationToken cancellationToken) =>
            Task.FromResult<RegisteredDevice?>(trackedDevice);
        public Task<RepositoryPage<RegisteredDevice>> GetPageAsync(RegisteredDeviceQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<AuditLog>> GetHistoryAsync(Guid deviceId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public void Add(RegisteredDevice device) => throw new NotSupportedException();
        public void Add(DeviceChallenge deviceChallenge) => throw new NotSupportedException();
    }

    private sealed class RecordingDeviceSessionTokenService : IDeviceSessionTokenService
    {
        public bool WasCreated { get; private set; }
        public (string Token, DateTimeOffset ExpiresAt) Create(Guid registeredDeviceId, Guid userId)
        {
            WasCreated = true;
            return ("token", Now.AddMinutes(5));
        }
        public DeviceSessionPrincipal? Validate(string token) => throw new NotSupportedException();
    }

    private sealed class CurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;
        public string? Email => "operator@tests.local";
        public string? FullName => "Operator";
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => false;
    }

    private sealed class AuditRepository : IAuditLogRepository
    {
        public void Add(AuditLog auditLog) { }
    }

    private sealed class UnitOfWork(Action? onSave = null) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            onSave?.Invoke();
            return Task.FromResult(1);
        }
        public void ClearChanges() { }
    }
}
