using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.RegisteredDevices;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Domain.Security;
using TransjapHorimetros.Infrastructure.Persistence;

namespace TransjapHorimetros.IntegrationTests;

public sealed class RegisteredDeviceIntegrationTests(TransjapApiFactory factory) : IClassFixture<TransjapApiFactory>
{
    [Fact]
    public async Task RegisterSameIdSameKey_IsIdempotent()
    {
        await factory.ResetDatabaseAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);

        using var firstResponse = await RegisterDeviceAsync(operatorClient, localDeviceId, key, "Tablet Campo");
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        var deviceId = firstBody.GetProperty("id").GetGuid();
        Assert.Equal("PENDING", firstBody.GetProperty("status").GetString());

        using var retryResponse = await RegisterDeviceAsync(operatorClient, localDeviceId, key, "Nome Atualizado No Retry");
        Assert.True(retryResponse.IsSuccessStatusCode);
        var retryBody = await retryResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(deviceId, retryBody.GetProperty("id").GetGuid());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.Equal(1, await db.RegisteredDevices.CountAsync(d => d.LocalDeviceId == localDeviceId));
    }

    [Fact]
    public async Task RegisterSameIdDifferentKey_Returns409()
    {
        await factory.ResetDatabaseAsync();
        using var key1 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var key2 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);

        using var firstResponse = await RegisterDeviceAsync(operatorClient, localDeviceId, key1);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var conflictResponse = await RegisterDeviceAsync(operatorClient, localDeviceId, key2);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
    }

    [Fact]
    public async Task RegisterSameKeyDifferentId_Returns409()
    {
        await factory.ResetDatabaseAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId1 = $"web-{Guid.NewGuid():D}";
        var localDeviceId2 = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);

        using var firstResponse = await RegisterDeviceAsync(operatorClient, localDeviceId1, key);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var conflictResponse = await RegisterDeviceAsync(operatorClient, localDeviceId2, key);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
    }

    [Fact]
    public async Task PendingDevice_CannotCreateChallenge()
    {
        await factory.ResetDatabaseAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);

        using var registration = await RegisterDeviceAsync(operatorClient, localDeviceId, key);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        using var challengeResponse = await operatorClient.PostAsJsonAsync("/api/v1/devices/challenge", new { localDeviceId });
        Assert.Equal(HttpStatusCode.Forbidden, challengeResponse.StatusCode);
    }

    [Fact]
    public async Task ChallengeExpires()
    {
        await factory.ResetDatabaseAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);

        var (deviceId, _) = await RegisterAndAuthorizeDeviceAsync(operatorClient, managerClient, localDeviceId, key);
        var (challengeId, payload) = await CreateChallengeAsync(operatorClient, localDeviceId);

        // Simula expiração do desafio avançando no banco de testes
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
            var challenge = await db.DeviceChallenges.FirstAsync(c => c.Id == challengeId);
            challenge.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        var signature = SignPayload(key, payload);
        using var verifyResponse = await operatorClient.PostAsJsonAsync("/api/v1/devices/challenge/verify", new
        {
            challengeId,
            signature,
        });

        Assert.Equal(HttpStatusCode.Conflict, verifyResponse.StatusCode);
    }

    [Fact]
    public async Task ChallengeReplay_Returns409()
    {
        await factory.ResetDatabaseAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);

        await RegisterAndAuthorizeDeviceAsync(operatorClient, managerClient, localDeviceId, key);
        var (challengeId, payload) = await CreateChallengeAsync(operatorClient, localDeviceId);
        var signature = SignPayload(key, payload);

        using var firstVerify = await operatorClient.PostAsJsonAsync("/api/v1/devices/challenge/verify", new
        {
            challengeId,
            signature,
        });
        firstVerify.EnsureSuccessStatusCode();

        using var replayVerify = await operatorClient.PostAsJsonAsync("/api/v1/devices/challenge/verify", new
        {
            challengeId,
            signature,
        });
        Assert.Equal(HttpStatusCode.Conflict, replayVerify.StatusCode);
    }

    [Fact]
    public async Task WrongSignature_Returns403()
    {
        await factory.ResetDatabaseAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);

        await RegisterAndAuthorizeDeviceAsync(operatorClient, managerClient, localDeviceId, key);
        var (challengeId, _) = await CreateChallengeAsync(operatorClient, localDeviceId);

        using var verifyResponse = await operatorClient.PostAsJsonAsync("/api/v1/devices/challenge/verify", new
        {
            challengeId,
            signature = Convert.ToBase64String(new byte[64]),
        });
        Assert.Equal(HttpStatusCode.Forbidden, verifyResponse.StatusCode);

        // O desafio consumido por assinatura inválida não pode ser reutilizado
        using var retryResponse = await operatorClient.PostAsJsonAsync("/api/v1/devices/challenge/verify", new
        {
            challengeId,
            signature = Convert.ToBase64String(new byte[64]),
        });
        Assert.Equal(HttpStatusCode.Conflict, retryResponse.StatusCode);
    }

    [Fact]
    public async Task TamperedPayloadSignature_Fails()
    {
        await factory.ResetDatabaseAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);

        await RegisterAndAuthorizeDeviceAsync(operatorClient, managerClient, localDeviceId, key);
        var (challengeId, payload) = await CreateChallengeAsync(operatorClient, localDeviceId);

        // Assinatura sobre payload adulterado
        var tamperedSignature = SignPayload(key, payload + "\nTAMPERED");

        using var verifyResponse = await operatorClient.PostAsJsonAsync("/api/v1/devices/challenge/verify", new
        {
            challengeId,
            signature = tamperedSignature,
        });
        Assert.Equal(HttpStatusCode.Forbidden, verifyResponse.StatusCode);
    }

    [Fact]
    public async Task DeviceSessionExpires()
    {
        await factory.ResetDatabaseAsync();
        SetDeviceEnforcement(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);

        var (deviceId, _) = await RegisterAndAuthorizeDeviceAsync(operatorClient, managerClient, localDeviceId, key);

        // Cria token de sessão expirado (ExpiresAt no passado)
        string expiredToken;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email!.StartsWith("operator"));
            var payload = new
            {
                DeviceId = deviceId,
                UserId = user.Id,
                ExpiresAtUnix = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds(),
                Jti = Guid.NewGuid(),
            };
            var encodedPayload = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
            var signature = Base64Url(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes("transjap-phase-3-integration-tests-signing-key-2026"),
                Encoding.ASCII.GetBytes(encodedPayload)));
            expiredToken = $"{encodedPayload}.{signature}";
        }

        var machineId = await GetFirstMachineIdAsync();
        var readingRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/readings")
        {
            Content = JsonContent.Create(new
            {
                machineId,
                value = 100m,
                readingType = "INTERMEDIATE",
                capturedAtDevice = DateTimeOffset.UtcNow,
                clientEventId = Guid.NewGuid(),
            }),
        };
        readingRequest.Headers.Add("X-Device-Session", expiredToken);

        using var response = await operatorClient.SendAsync(readingRequest);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
            Assert.Equal(0, await db.HourMeterReadings.CountAsync());
        }
    }

    [Fact]
    public async Task DeviceSessionBelongsToUser()
    {
        await factory.ResetDatabaseAsync();
        SetDeviceEnforcement(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var supervisorClient = factory.CreateClientForRole(AppRoles.Supervisor);
        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);

        await RegisterAndAuthorizeDeviceAsync(operatorClient, managerClient, localDeviceId, key);
        var (challengeId, payload) = await CreateChallengeAsync(operatorClient, localDeviceId);
        var sessionToken = await VerifyChallengeAndGetTokenAsync(operatorClient, challengeId, SignPayload(key, payload));

        // Supervisor tenta usar a sessão emitida para o Operador
        var machineId = await GetFirstMachineIdAsync();
        var readingRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/readings")
        {
            Content = JsonContent.Create(new
            {
                machineId,
                value = 100m,
                readingType = "INTERMEDIATE",
                capturedAtDevice = DateTimeOffset.UtcNow,
                clientEventId = Guid.NewGuid(),
            }),
        };
        readingRequest.Headers.Add("X-Device-Session", sessionToken);

        using var response = await supervisorClient.SendAsync(readingRequest);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RevokedDeviceRejectsOldSession()
    {
        await factory.ResetDatabaseAsync();
        SetDeviceEnforcement(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);

        var (deviceId, _) = await RegisterAndAuthorizeDeviceAsync(operatorClient, managerClient, localDeviceId, key);
        var (challengeId, payload) = await CreateChallengeAsync(operatorClient, localDeviceId);
        var sessionToken = await VerifyChallengeAndGetTokenAsync(operatorClient, challengeId, SignPayload(key, payload));

        // Administrador/Gerente revoga o dispositivo
        using var revokeResponse = await managerClient.PostAsync($"/api/v1/devices/{deviceId}/revoke", null);
        revokeResponse.EnsureSuccessStatusCode();

        // Envia leitura usando a sessão antiga emitida antes da revogação
        var machineId = await GetFirstMachineIdAsync();
        var readingRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/readings")
        {
            Content = JsonContent.Create(new
            {
                machineId,
                value = 100m,
                readingType = "INTERMEDIATE",
                capturedAtDevice = DateTimeOffset.UtcNow,
                clientEventId = Guid.NewGuid(),
            }),
        };
        readingRequest.Headers.Add("X-Device-Session", sessionToken);

        using var response = await operatorClient.SendAsync(readingRequest);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Confirma no banco: nenhuma leitura criada
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.Equal(0, await db.HourMeterReadings.CountAsync());
    }

    [Fact]
    public async Task ReadingPersistsRegisteredDeviceId()
    {
        await factory.ResetDatabaseAsync();
        SetDeviceEnforcement(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);

        var (deviceId, _) = await RegisterAndAuthorizeDeviceAsync(operatorClient, managerClient, localDeviceId, key);
        var (challengeId, payload) = await CreateChallengeAsync(operatorClient, localDeviceId);
        var sessionToken = await VerifyChallengeAndGetTokenAsync(operatorClient, challengeId, SignPayload(key, payload));

        var machineId = await GetFirstMachineIdAsync();
        var clientEventId = Guid.NewGuid();
        var readingRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/readings")
        {
            Content = JsonContent.Create(new
            {
                machineId,
                value = 150m,
                readingType = "INTERMEDIATE",
                capturedAtDevice = DateTimeOffset.UtcNow,
                clientEventId,
                deviceId = localDeviceId,
            }),
        };
        readingRequest.Headers.Add("X-Device-Session", sessionToken);

        using var response = await operatorClient.SendAsync(readingRequest);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var readingBody = await response.Content.ReadFromJsonAsync<JsonElement>();
        var readingId = readingBody.GetProperty("id").GetGuid();

        // Consulta direta no SQL test database para confirmar RegisteredDeviceId server-side
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var persistedReading = await db.HourMeterReadings.FirstOrDefaultAsync(r => r.Id == readingId);
        Assert.NotNull(persistedReading);
        Assert.Equal(deviceId, persistedReading.RegisteredDeviceId);
    }

    [Fact]
    public async Task ClientCannotSetRegisteredDeviceId()
    {
        await factory.ResetDatabaseAsync();
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        var machineId = await GetFirstMachineIdAsync();

        // Cliente tenta forjar registeredDeviceId no JSON body
        using var response = await operatorClient.PostAsJsonAsync("/api/v1/readings", new
        {
            machineId,
            value = 100m,
            readingType = "INTERMEDIATE",
            capturedAtDevice = DateTimeOffset.UtcNow,
            clientEventId = Guid.NewGuid(),
            registeredDeviceId = Guid.NewGuid(),
        });

        // Como CreateReadingRequest usa JsonUnmappedMemberHandling.Disallow, deve retornar 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PhotoRequiresDeviceSession()
    {
        await factory.ResetDatabaseAsync();
        SetDeviceEnforcement(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);

        var (_, sessionToken) = await RegisterAndAuthorizeDeviceAsync(operatorClient, managerClient, localDeviceId, key);
        var (challengeId, payload) = await CreateChallengeAsync(operatorClient, localDeviceId);
        var validSession = await VerifyChallengeAndGetTokenAsync(operatorClient, challengeId, SignPayload(key, payload));

        var machineId = await GetFirstMachineIdAsync();
        var readingId = await CreateReadingWithSessionAsync(operatorClient, machineId, validSession);

        // Upload de foto sem enviar X-Device-Session
        using var photoForm = CreatePhotoForm(CreateJpeg());
        using var photoResponse = await operatorClient.PostAsync($"/api/v1/readings/{readingId}/photo", photoForm);

        Assert.Equal(HttpStatusCode.Forbidden, photoResponse.StatusCode);

        // Confirma no banco: nenhuma PhotoEvidence criada
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.Equal(0, await db.PhotoEvidences.CountAsync(p => p.ReadingId == readingId));
    }

    [Fact]
    public async Task PhotoDeviceMustMatchReadingDevice()
    {
        await factory.ResetDatabaseAsync();
        SetDeviceEnforcement(true);
        using var keyA = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var keyB = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceIdA = $"web-a-{Guid.NewGuid():D}";
        var localDeviceIdB = $"web-b-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);

        // Cadastra e autoriza Device A e Device B
        await RegisterAndAuthorizeDeviceAsync(operatorClient, managerClient, localDeviceIdA, keyA);
        await RegisterAndAuthorizeDeviceAsync(operatorClient, managerClient, localDeviceIdB, keyB);

        // Obtém sessão para Device A e cria leitura
        var (challengeIdA, payloadA) = await CreateChallengeAsync(operatorClient, localDeviceIdA);
        var sessionA = await VerifyChallengeAndGetTokenAsync(operatorClient, challengeIdA, SignPayload(keyA, payloadA));
        var machineId = await GetFirstMachineIdAsync();
        var readingId = await CreateReadingWithSessionAsync(operatorClient, machineId, sessionA);

        // Obtém sessão para Device B
        var (challengeIdB, payloadB) = await CreateChallengeAsync(operatorClient, localDeviceIdB);
        var sessionB = await VerifyChallengeAndGetTokenAsync(operatorClient, challengeIdB, SignPayload(keyB, payloadB));

        // Tenta enviar foto da leitura criada por Device A usando sessão do Device B
        using var photoForm = CreatePhotoForm(CreateJpeg());
        using var photoRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/readings/{readingId}/photo")
        {
            Content = photoForm,
        };
        photoRequest.Headers.Add("X-Device-Session", sessionB);

        using var photoResponse = await operatorClient.SendAsync(photoRequest);
        Assert.Equal(HttpStatusCode.Forbidden, photoResponse.StatusCode);

        // Nenhuma PhotoEvidence no banco
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.Equal(0, await db.PhotoEvidences.CountAsync(p => p.ReadingId == readingId));
    }

    [Fact]
    public async Task RevokedDeviceCannotUploadPendingPhoto()
    {
        await factory.ResetDatabaseAsync();
        SetDeviceEnforcement(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);

        var (deviceId, _) = await RegisterAndAuthorizeDeviceAsync(operatorClient, managerClient, localDeviceId, key);
        var (challengeId, payload) = await CreateChallengeAsync(operatorClient, localDeviceId);
        var sessionToken = await VerifyChallengeAndGetTokenAsync(operatorClient, challengeId, SignPayload(key, payload));

        var machineId = await GetFirstMachineIdAsync();
        var readingId = await CreateReadingWithSessionAsync(operatorClient, machineId, sessionToken);

        // Administrador revoga o dispositivo antes do upload da foto
        using var revokeResponse = await managerClient.PostAsync($"/api/v1/devices/{deviceId}/revoke", null);
        revokeResponse.EnsureSuccessStatusCode();

        // Upload de foto usando a sessão antiga
        using var photoForm = CreatePhotoForm(CreateJpeg());
        using var photoRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/readings/{readingId}/photo")
        {
            Content = photoForm,
        };
        photoRequest.Headers.Add("X-Device-Session", sessionToken);

        using var photoResponse = await operatorClient.SendAsync(photoRequest);
        Assert.Equal(HttpStatusCode.Forbidden, photoResponse.StatusCode);

        // Confirma banco: nenhuma evidência criada
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.Equal(0, await db.PhotoEvidences.CountAsync(p => p.ReadingId == readingId));
    }

    [Fact]
    public async Task OperatorCannotManageDevices()
    {
        await factory.ResetDatabaseAsync();
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        var fakeId = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync("/api/v1/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.PostAsync($"/api/v1/devices/{fakeId}/authorize", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.PostAsync($"/api/v1/devices/{fakeId}/revoke", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.PatchAsJsonAsync($"/api/v1/devices/{fakeId}", new { displayName = "Novo" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync($"/api/v1/devices/{fakeId}/history")).StatusCode);
    }

    [Fact]
    public async Task ManagerCanManageDevices()
    {
        await factory.ResetDatabaseAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);

        var (deviceId, _) = await RegisterDeviceAsync(operatorClient, localDeviceId, key);

        // Listar
        var listResponse = await managerClient.GetAsync("/api/v1/devices");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        // Autorizar
        var authResponse = await managerClient.PostAsync($"/api/v1/devices/{deviceId}/authorize", null);
        Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);

        // Renomear
        var renameResponse = await managerClient.PatchAsJsonAsync($"/api/v1/devices/{deviceId}", new { displayName = "Renomeado" });
        Assert.Equal(HttpStatusCode.OK, renameResponse.StatusCode);

        // Histórico
        var historyResponse = await managerClient.GetAsync($"/api/v1/devices/{deviceId}/history");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        // Revogar
        var revokeResponse = await managerClient.PostAsync($"/api/v1/devices/{deviceId}/revoke", null);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
    }

    [Fact]
    public async Task AdminCanManageDevices()
    {
        await factory.ResetDatabaseAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var adminClient = factory.CreateClientForRole(AppRoles.Admin);

        var (deviceId, _) = await RegisterDeviceAsync(operatorClient, localDeviceId, key);

        var listResponse = await adminClient.GetAsync("/api/v1/devices");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var authResponse = await adminClient.PostAsync($"/api/v1/devices/{deviceId}/authorize", null);
        Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);

        var renameResponse = await adminClient.PatchAsJsonAsync($"/api/v1/devices/{deviceId}", new { displayName = "Admin Renomeado" });
        Assert.Equal(HttpStatusCode.OK, renameResponse.StatusCode);

        var historyResponse = await adminClient.GetAsync($"/api/v1/devices/{deviceId}/history");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        var revokeResponse = await adminClient.PostAsync($"/api/v1/devices/{deviceId}/revoke", null);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
    }

    [Fact]
    public async Task RevokedDeviceCannotBeReauthorized()
    {
        await factory.ResetDatabaseAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);

        var (deviceId, _) = await RegisterAndAuthorizeDeviceAsync(operatorClient, managerClient, localDeviceId, key);

        using var revokeResponse = await managerClient.PostAsync($"/api/v1/devices/{deviceId}/revoke", null);
        revokeResponse.EnsureSuccessStatusCode();

        // Tentativa de reautorizar dispositivo revogado retorna 409
        using var reauthResponse = await managerClient.PostAsync($"/api/v1/devices/{deviceId}/authorize", null);
        Assert.Equal(HttpStatusCode.Conflict, reauthResponse.StatusCode);

        // Confirma estado continua REVOKED
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var device = await db.RegisteredDevices.FindAsync(deviceId);
        Assert.NotNull(device);
        Assert.Equal(RegisteredDeviceStatus.Revoked, device.Status);
    }

    [Fact]
    public async Task DeviceStatus_HidesAdministrativeEmailsFromOperator()
    {
        await factory.ResetDatabaseAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var localDeviceId = $"web-{Guid.NewGuid():D}";
        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);

        var (deviceId, _) = await RegisterAndAuthorizeDeviceAsync(operatorClient, managerClient, localDeviceId, key);

        // Consulta de status do próprio dispositivo
        using var statusResponse = await operatorClient.GetAsync($"/api/v1/devices/status?localDeviceId={localDeviceId}");
        statusResponse.EnsureSuccessStatusCode();
        var json = await statusResponse.Content.ReadAsStringAsync();

        // Verifica que o JSON não expõe emails administrativos
        Assert.DoesNotContain("requestedByEmail", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorizedByEmail", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("revokedByEmail", json, StringComparison.OrdinalIgnoreCase);

        var statusObj = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal(deviceId, statusObj.GetProperty("id").GetGuid());
        Assert.Equal("AUTHORIZED", statusObj.GetProperty("status").GetString());
    }

    private void SetDeviceEnforcement(bool enforce)
    {
        var options = factory.Services.GetRequiredService<DeviceIdentityOptions>();
        options.EnforceForReadings = enforce;
    }

    private async Task<Guid> GetFirstMachineIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var machine = await db.Machines.FirstAsync(m => m.MeterUnit != null);
        return machine.Id;
    }

    private static async Task<HttpResponseMessage> RegisterDeviceAsync(
        HttpClient client, string localDeviceId, ECDsa key, string displayName = "Tablet Campo")
    {
        return await client.PostAsJsonAsync("/api/v1/devices/registration", new
        {
            localDeviceId,
            displayName,
            platform = "WEB_PWA",
            publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            keyAlgorithm = "ECDSA_P256_SHA256",
        });
    }

    private static async Task<(Guid Id, HttpResponseMessage Response)> RegisterAndAuthorizeDeviceAsync(
        HttpClient operatorClient, HttpClient managerClient, string localDeviceId, ECDsa key)
    {
        var regResponse = await RegisterDeviceAsync(operatorClient, localDeviceId, key);
        regResponse.EnsureSuccessStatusCode();
        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonElement>();
        var deviceId = regBody.GetProperty("id").GetGuid();

        var authResponse = await managerClient.PostAsync($"/api/v1/devices/{deviceId}/authorize", null);
        authResponse.EnsureSuccessStatusCode();

        return (deviceId, authResponse);
    }

    private static async Task<(Guid Id, string Payload)> CreateChallengeAsync(HttpClient client, string localDeviceId)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/devices/challenge", new { localDeviceId });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("challengeId").GetGuid(), body.GetProperty("payload").GetString()!);
    }

    private static string SignPayload(ECDsa key, string payload)
    {
        var signature = key.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Convert.ToBase64String(signature);
    }

    private static async Task<string> VerifyChallengeAndGetTokenAsync(
        HttpClient client, Guid challengeId, string signature)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/devices/challenge/verify", new
        {
            challengeId,
            signature,
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("deviceSessionToken").GetString()!;
    }

    private static async Task<Guid> CreateReadingWithSessionAsync(
        HttpClient client, Guid machineId, string sessionToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/readings")
        {
            Content = JsonContent.Create(new
            {
                machineId,
                value = 100m,
                readingType = "INTERMEDIATE",
                capturedAtDevice = DateTimeOffset.UtcNow,
                clientEventId = Guid.NewGuid(),
            }),
        };
        request.Headers.Add("X-Device-Session", sessionToken);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private static MultipartFormDataContent CreatePhotoForm(byte[] content, Guid? clientEventId = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "proof.jpg");
        form.Add(new StringContent((clientEventId ?? Guid.NewGuid()).ToString()), "clientEventId");
        form.Add(new StringContent(DateTimeOffset.UtcNow.ToString("O")), "capturedAtDevice");
        form.Add(new StringContent("integration-test-device"), "deviceId");
        return form;
    }

    private static byte[] CreateJpeg() =>
    [
        0xff, 0xd8,
        0xff, 0xc0, 0x00, 0x11, 0x08,
        0x00, 0x01,
        0x00, 0x02,
        0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x00, 0x03, 0x11, 0x00,
        0xff, 0xd9,
    ];

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
