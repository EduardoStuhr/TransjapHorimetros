using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransjapHorimetros.Application.Ocr;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Domain.Security;
using TransjapHorimetros.Infrastructure.Persistence;

namespace TransjapHorimetros.IntegrationTests;

public sealed class PhotoOcrIntegrationTests(TransjapApiFactory factory) : IClassFixture<TransjapApiFactory>
{
    [Fact]
    public async Task Routes_EnforceAuthenticationAndRoles()
    {
        using var anonymous = factory.CreateAnonymousClient();
        var route = $"/api/v1/readings/{Guid.NewGuid()}/photo/ocr";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(route + "/reprocess", null)).StatusCode);
        using var op = factory.CreateClientForRole(AppRoles.Operator);
        Assert.Equal(HttpStatusCode.NotFound, (await op.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync(route + "/history")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsync(route + "/reprocess", null)).StatusCode);
    }

    [Theory]
    [InlineData("success", "SUCCEEDED", null)]
    [InlineData("low", "LOW_CONFIDENCE", AnomalyType.LowOcrConfidence)]
    [InlineData("divergence", "SUCCEEDED", AnomalyType.OcrOperatorDivergence)]
    [InlineData("no-value", "NO_VALUE_DETECTED", null)]
    [InlineData("timeout", "FAILED", null)]
    [InlineData("429", "FAILED", null)]
    [InlineData("500", "FAILED", null)]
    [InlineData("invalid", "FAILED", null)]
    public async Task Upload_PersistsOcrWithoutChangingReadingOrPhoto(string scenario, string status, AnomalyType? anomaly)
    {
        await factory.ResetDatabaseAsync();
        factory.Services.GetRequiredService<FakePhotoOcrProvider>().Scenario = scenario;
        using var client = await CreateClientAsync(AppRoles.Admin);
        var readingId = await UploadAsync(client);
        var result = await client.GetFromJsonAsync<JsonElement>(Route(readingId));
        Assert.Equal(status, result.GetProperty("status").GetString());
        Assert.False(result.TryGetProperty("rawText", out _));
        Assert.False(result.TryGetProperty("key", out _));
        var reading = await client.GetFromJsonAsync<JsonElement>($"/api/v1/readings/{readingId}");
        Assert.Equal(100m, reading.GetProperty("value").GetDecimal());
        var photo = await client.GetFromJsonAsync<JsonElement>($"/api/v1/readings/{readingId}/photo");
        Assert.Equal("ACTIVE", photo.GetProperty("status").GetString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var saved = await db.PhotoOcrResults.SingleAsync();
        Assert.Equal(result.GetProperty("id").GetGuid(), saved.Id);
        if (anomaly.HasValue)
            Assert.Equal(1, await db.Anomalies.CountAsync(a => a.OcrResultId == saved.Id && a.Type == anomaly.Value));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == saved.Id.ToString()
            && a.Action == (status == "FAILED" ? "PHOTO_OCR_FAILED" : "PHOTO_OCR_PROCESSED")));
    }

    [Theory]
    [InlineData(AppRoles.Supervisor)]
    [InlineData(AppRoles.Administrative)]
    [InlineData(AppRoles.Manager)]
    [InlineData(AppRoles.Admin)]
    public async Task Reprocess_NewResultPreservesHistoryAndEnforcesCooldown(string role)
    {
        await factory.ResetDatabaseAsync();
        using var client = await CreateClientAsync(role);
        var readingId = await UploadAsync(client);
        using var early = await client.PostAsync(Route(readingId) + "/reprocess", null);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        await Task.Delay(1100);
        using var reprocess = await client.PostAsync(Route(readingId) + "/reprocess", null);
        reprocess.EnsureSuccessStatusCode();
        var history = await client.GetFromJsonAsync<JsonElement[]>(Route(readingId) + "/history");
        Assert.Equal(2, history!.Length);
        Assert.NotEqual(history[0].GetProperty("id").GetGuid(), history[1].GetProperty("id").GetGuid());
        var latest = await client.GetFromJsonAsync<JsonElement>(Route(readingId));
        Assert.Equal(history[0].GetProperty("id").GetGuid(), latest.GetProperty("id").GetGuid());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "PHOTO_OCR_REPROCESSED"));
    }

    [Fact]
    public async Task ConcurrentInitialCalls_AndGet_DoNotInvokeProviderAgain()
    {
        await factory.ResetDatabaseAsync();
        using var client = await CreateClientAsync(AppRoles.Admin);
        var readingId = await UploadAsync(client);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IPhotoOcrService>().ProcessAsync(readingId, false, "retry", default);
        }));
        await client.GetAsync(Route(readingId));
        await client.GetAsync(Route(readingId) + "/history");
        Assert.Equal(1, factory.Services.GetRequiredService<FakePhotoOcrProvider>().Calls);
        using var dbScope = factory.Services.CreateScope();
        Assert.Equal(1, await dbScope.ServiceProvider.GetRequiredService<TransjapDbContext>().PhotoOcrResults.CountAsync());
    }

    [Fact]
    public async Task ConcurrentManualClaims_AllowOnlyOneNewExecution()
    {
        await factory.ResetDatabaseAsync();
        using var client = await CreateClientAsync(AppRoles.Admin);
        var readingId = await UploadAsync(client);
        await Task.Delay(1100);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsync(Route(readingId) + "/reprocess", null)));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var response in responses) response.Dispose();
    }

    private async Task<HttpClient> CreateClientAsync(string role)
    {
        var user = await factory.CreateUserAsync($"ocr.{Guid.NewGuid():N}@tests.local", "Test!Password2026", true, role);
        var client = factory.CreateAnonymousClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = "Test!Password2026" });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string Route(Guid id) => $"/api/v1/readings/{id}/photo/ocr";
    private static async Task<Guid> UploadAsync(HttpClient client)
    {
        var machines = await client.GetFromJsonAsync<JsonElement>("/api/v1/machines?page=1&pageSize=1");
        using var created = await client.PostAsJsonAsync("/api/v1/readings", new {
            machineId = machines.GetProperty("items")[0].GetProperty("id").GetGuid(), value = 100,
            readingType = "INTERMEDIATE", capturedAtDevice = DateTimeOffset.UtcNow, clientEventId = Guid.NewGuid(),
        });
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent([0xff,0xd8,0xff,0xc0,0,0x11,8,0,1,0,2,3,1,0x11,0,2,0x11,0,3,0x11,0,0xff,0xd9]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "evidence.jpg");
        using var upload = await client.PostAsync($"/api/v1/readings/{id}/photo", form);
        upload.EnsureSuccessStatusCode();
        return id;
    }
}
