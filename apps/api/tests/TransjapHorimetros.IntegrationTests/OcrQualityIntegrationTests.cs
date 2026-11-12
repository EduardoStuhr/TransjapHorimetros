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
using TransjapHorimetros.Infrastructure.Persistence.Repositories;

namespace TransjapHorimetros.IntegrationTests;

public sealed class OcrQualityIntegrationTests(TransjapApiFactory factory) : IClassFixture<TransjapApiFactory>
{
    [Theory]
    [InlineData("missing-confidence", "LOW_CONFIDENCE", "CONFIDENCE_MISSING")]
    [InlineData("ambiguous", "NO_VALUE_DETECTED", "AMBIGUOUS_VALUES")]
    [InlineData("brazilian", "SUCCEEDED", "SELECTED")]
    public async Task QualityClassificationPersistsCandidatesAndPreservesEvidence(string scenario, string status, string reason)
    {
        await factory.ResetDatabaseAsync();
        factory.Services.GetRequiredService<FakePhotoOcrProvider>().Scenario = scenario;
        using var client = factory.CreateClient();
        var id = await Upload(client);
        var result = await client.GetFromJsonAsync<JsonElement>(Route(id));
        Assert.Equal(status, result.GetProperty("status").GetString());
        Assert.Equal(reason, result.GetProperty("reviewReason").GetString());
        Assert.Equal("meter-normalization-v2", result.GetProperty("algorithmVersion").GetString());
        Assert.True(result.GetProperty("candidates").GetArrayLength() > 0);
        if (scenario == "ambiguous") Assert.Equal(JsonValueKind.Null, result.GetProperty("detectedValue").ValueKind);
        if (scenario == "missing-confidence") Assert.Equal(JsonValueKind.Null, result.GetProperty("confidence").ValueKind);
        if (scenario == "brazilian") Assert.Equal(1250.5m, result.GetProperty("detectedValue").GetDecimal());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.Equal(100m, (await db.HourMeterReadings.SingleAsync(r => r.Id == id)).Value);
        Assert.Equal(PhotoEvidenceStatus.Active, (await db.PhotoEvidences.SingleAsync(p => p.ReadingId == id)).Status);
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "PHOTO_OCR_PROCESSED"));
    }

    [Fact]
    public async Task HighResolutionEvidenceIsPreservedAndNotSentToTheProvider()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var id = await Upload(client, oversized: true);
        var result = await client.GetFromJsonAsync<JsonElement>(Route(id));
        Assert.Equal("FAILED", result.GetProperty("status").GetString());
        Assert.Equal("OCR_IMAGE_LIMIT", result.GetProperty("errorCode").GetString());
        Assert.Equal(0, factory.Services.GetRequiredService<FakePhotoOcrProvider>().Calls);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.Equal(14000, (await db.PhotoEvidences.SingleAsync(p => p.ReadingId == id)).Width);
        Assert.Equal(100m, (await db.HourMeterReadings.SingleAsync(r => r.Id == id)).Value);
    }

    [Fact]
    public async Task ReprocessKeyIsIdempotentAndResolvedAlertsStayResolved()
    {
        await factory.ResetDatabaseAsync();
        factory.Services.GetRequiredService<FakePhotoOcrProvider>().Scenario = "divergence";
        using var client = factory.CreateClient();
        var id = await Upload(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
            var alert = await db.Anomalies.SingleAsync(a => a.ReadingId == id);
            var decision = await client.PostAsJsonAsync($"/api/v1/operations/anomalies/{alert.Id}/review",
                new { expectedStatus = "OPEN", status = "RESOLVED", reason = "Conferida a fotografia de teste" });
            Assert.Equal(HttpStatusCode.OK, decision.StatusCode);
        }
        await Task.Delay(1100);
        var key = Guid.NewGuid();
        async Task<HttpResponseMessage> Reprocess()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Route(id) + "/reprocess");
            request.Headers.Add("Idempotency-Key", key.ToString());
            return await client.SendAsync(request);
        }
        var responses = await Task.WhenAll(Reprocess(), Reprocess());
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var replay = await Reprocess();
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var body = await replay.Content.ReadFromJsonAsync<JsonElement>();
        using var final = factory.Services.CreateScope();
        var database = final.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var saved = await database.PhotoOcrResults.SingleAsync(r => r.RequestId == key);
        Assert.Equal(saved.Id, body.GetProperty("id").GetGuid());
        Assert.Equal(2, await database.PhotoOcrResults.CountAsync());
        Assert.Equal(1, await database.AuditLogs.CountAsync(a => a.Action == "PHOTO_OCR_REPROCESSED"));
        Assert.Equal(1, await database.AuditLogs.CountAsync(a => a.Action == "ANOMALY_REVIEWED"));
        Assert.Equal(AnomalyStatus.Resolved, (await database.Anomalies.SingleAsync(a => a.ReadingId == id)).Status);
        Assert.Equal(100m, (await database.HourMeterReadings.SingleAsync(r => r.Id == id)).Value);
    }

    [Fact]
    public async Task OperatorsCanOnlyReadTheirOwnEvidenceAndCannotReadAdministrativeOcr()
    {
        await factory.ResetDatabaseAsync();
        using var owner = factory.CreateClientForRole(AppRoles.Operator);
        using var other = factory.CreateClientForRole(AppRoles.Operator);
        using var supervisor = factory.CreateClientForRole(AppRoles.Supervisor);
        var id = await Upload(owner);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/readings/{id}/photo/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/readings/{id}/photo")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/readings/{id}/photo/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(Route(id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(Route(id) + "/history")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsync(Route(id) + "/reprocess", null)).StatusCode);
        var publicResult = await owner.GetFromJsonAsync<JsonElement>(Route(id));
        Assert.False(publicResult.TryGetProperty("rawText", out _));
        Assert.False(publicResult.TryGetProperty("candidates", out _));
        var administrative = await supervisor.GetFromJsonAsync<JsonElement[]>(Route(id) + "/history");
        Assert.True(administrative![0].TryGetProperty("rawText", out _));
        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync($"/api/v1/readings/{id}/photo/content")).StatusCode);
    }

    [Fact]
    public async Task AbandonedProcessingBecomesAnAuditedFailureWithoutChangingCompletedHistory()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var reading = await Upload(client);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var photo = await db.PhotoEvidences.SingleAsync(p => p.ReadingId == reading);
        var abandoned = new TransjapHorimetros.Domain.Entities.PhotoOcrResult(photo.Id, "FakeTests", false,
            DateTimeOffset.UtcNow.AddMinutes(-10), "interrupted", null);
        db.PhotoOcrResults.Add(abandoned);
        await db.SaveChangesAsync();
        await new PhotoOcrRepository(db).RecoverExpiredAsync(photo.Id, new OcrOptions(), DateTimeOffset.UtcNow, default);
        Assert.Equal(PhotoOcrStatus.Failed, abandoned.Status);
        Assert.Equal("OCR_INTERRUPTED", abandoned.ErrorCode);
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityId == abandoned.Id.ToString() && a.Action == "PHOTO_OCR_FAILED"));
        Assert.Equal(2, await db.PhotoOcrResults.CountAsync());
        Assert.Equal(100m, (await db.HourMeterReadings.SingleAsync()).Value);
    }

    internal static string Route(Guid id) => $"/api/v1/readings/{id}/photo/ocr";
    internal static async Task<Guid> Upload(HttpClient client, bool oversized = false)
    {
        var machine = await client.GetFromJsonAsync<JsonElement>("/api/v1/machines/by-fleet/68");
        using var created = await client.PostAsJsonAsync("/api/v1/readings", new {
            machineId = machine.GetProperty("id").GetGuid(), value = 100, readingType = "INTERMEDIATE",
            capturedAtDevice = DateTimeOffset.UtcNow, clientEventId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var form = new MultipartFormDataContent();
        byte[] image = [0xff,0xd8,0xff,0xc0,0,0x11,8,0,1,0,2,3,1,0x11,0,2,0x11,0,3,0x11,0,0xff,0xd9];
        if (oversized) { image[9] = 0x36; image[10] = 0xb0; }
        var file = new ByteArrayContent(image);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "synthetic-meter.jpg");
        using var upload = await client.PostAsync($"/api/v1/readings/{id}/photo", form);
        upload.EnsureSuccessStatusCode();
        return id;
    }
}

public sealed class OcrWorkerIntegrationTests
{
    [Fact]
    public async Task UploadReturnsWhileRecognitionIsRunningAndWorkerFinalizesTheOutcome()
    {
        await using var factory = new WorkerFactory();
        await factory.ResetDatabaseAsync();
        factory.Services.GetRequiredService<FakePhotoOcrProvider>().Scenario = "timeout";
        using var client = factory.CreateClient();
        var id = await OcrQualityIntegrationTests.Upload(client);
        // Recognition must still be unstarted or running when upload acknowledgement arrives.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
            Assert.False(await db.PhotoOcrResults.AnyAsync(r => r.Status == PhotoOcrStatus.Failed));
            Assert.Equal(1, await db.PhotoEvidences.CountAsync());
        }
        for (var attempt = 0; attempt < 80; attempt++)
        {
            var response = await client.GetAsync(OcrQualityIntegrationTests.Route(id));
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (result.GetProperty("status").GetString() == "FAILED")
                {
                    Assert.Equal("OCR_TIMEOUT", result.GetProperty("errorCode").GetString());
                    return;
                }
            }
            await Task.Delay(100);
        }
        Assert.Fail("The isolated worker did not finalize the timed-out OCR.");
    }

    private sealed class WorkerFactory : TransjapApiFactory
    {
        protected override bool BackgroundOcr => true;
        protected override int OcrTimeoutSeconds => 3;
    }
}
