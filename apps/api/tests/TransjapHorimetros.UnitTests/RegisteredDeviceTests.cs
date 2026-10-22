using Microsoft.Extensions.Options;
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
