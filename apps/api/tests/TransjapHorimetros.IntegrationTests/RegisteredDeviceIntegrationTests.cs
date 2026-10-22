using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Security;
using TransjapHorimetros.Infrastructure.Persistence;

namespace TransjapHorimetros.IntegrationTests;

public sealed class RegisteredDeviceIntegrationTests(TransjapApiFactory factory) : IClassFixture<TransjapApiFactory>
{
    [Fact]
    public async Task DeviceLifecycle_RequiresManagementRoleAndUsesOneTimeSignedChallenge()
    {
        await factory.ResetDatabaseAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);

        using var registration = await operatorClient.PostAsJsonAsync("/api/v1/devices/registration", new
        {
            localDeviceId,
            displayName = "Tablet campo",
            platform = "WEB_PWA",
            publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            keyAlgorithm = "ECDSA_P256_SHA256",
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var registered = await registration.Content.ReadFromJsonAsync<JsonElement>();
        var deviceId = registered.GetProperty("id").GetGuid();
        Assert.Equal("PENDING", registered.GetProperty("status").GetString());
        Assert.Equal(64, registered.GetProperty("publicKeyFingerprint").GetString()!.Length);

        using var retry = await operatorClient.PostAsJsonAsync("/api/v1/devices/registration", new
        {
            localDeviceId,
            displayName = "Nome ignorado no retry",
            platform = "WEB_PWA",
            publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            keyAlgorithm = "ECDSA_P256_SHA256",
        });
        Assert.True(retry.IsSuccessStatusCode);
        Assert.Equal(deviceId, (await retry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync("/api/v1/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.PostAsJsonAsync(
            "/api/v1/devices/challenge", new { localDeviceId })).StatusCode);

        using var authorized = await managerClient.PostAsync($"/api/v1/devices/{deviceId}/authorize", null);
        authorized.EnsureSuccessStatusCode();

        var challenge = await CreateChallengeAsync(operatorClient, localDeviceId);
        var signature = key.SignData(Encoding.UTF8.GetBytes(challenge.Payload), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        using var verification = await operatorClient.PostAsJsonAsync("/api/v1/devices/challenge/verify", new
        {
            challengeId = challenge.Id,
            signature = Convert.ToBase64String(signature),
        });
        verification.EnsureSuccessStatusCode();
        var session = await verification.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(session.GetProperty("deviceSessionToken").GetString()));

        using var replay = await operatorClient.PostAsJsonAsync("/api/v1/devices/challenge/verify", new
        {
            challengeId = challenge.Id,
            signature = Convert.ToBase64String(signature),
        });
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);

        var failedChallenge = await CreateChallengeAsync(operatorClient, localDeviceId);
        using var invalid = await operatorClient.PostAsJsonAsync("/api/v1/devices/challenge/verify", new
        {
            challengeId = failedChallenge.Id,
            signature = Convert.ToBase64String(new byte[64]),
        });
        Assert.Equal(HttpStatusCode.Forbidden, invalid.StatusCode);

        using var revoked = await managerClient.PostAsync($"/api/v1/devices/{deviceId}/revoke", null);
        revoked.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict,
            (await managerClient.PostAsync($"/api/v1/devices/{deviceId}/authorize", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await operatorClient.PostAsJsonAsync("/api/v1/devices/challenge", new { localDeviceId })).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(log => log.Action == AuditActions.DeviceRegistrationRequested));
        Assert.True(await db.AuditLogs.AnyAsync(log => log.Action == AuditActions.DeviceAuthorized));
        Assert.True(await db.AuditLogs.AnyAsync(log => log.Action == AuditActions.DeviceChallengeVerified));
        Assert.True(await db.AuditLogs.AnyAsync(log => log.Action == AuditActions.DeviceChallengeFailed));
        Assert.True(await db.AuditLogs.AnyAsync(log => log.Action == AuditActions.DeviceRevoked));
        Assert.Equal(1, await db.RegisteredDevices.CountAsync());
    }

    private static async Task<(Guid Id, string Payload)> CreateChallengeAsync(HttpClient client, string localDeviceId)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/devices/challenge", new { localDeviceId });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("challengeId").GetGuid(), body.GetProperty("payload").GetString()!);
    }
}
