using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Domain.Rules;
using TransjapHorimetros.Domain.Security;
using TransjapHorimetros.Infrastructure.Persistence;

namespace TransjapHorimetros.IntegrationTests;

public sealed class CompleteReadingModelIntegrationTests(TransjapApiFactory factory) : IClassFixture<TransjapApiFactory>
{
    [Fact]
    public async Task Create_RecordsServerAuthorSnapshotPreviousAndDelta_AndReplaysBeforeMutableValidation()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientForRole(AppRoles.Operator);
        var machineId = await MachineId(client);
        var now = DateTimeOffset.UtcNow;
        var first = await Create(client, machineId, 100.25m, now.AddMinutes(-2), device: "  device-6  ");
        var secondEvent = Guid.NewGuid();
        var second = await Create(client, machineId, 100.5m, now.AddMinutes(-1), eventId: secondEvent);
        Assert.Equal("HOURS", first.GetProperty("meterUnit").GetString());
        Assert.Equal("device-6", first.GetProperty("deviceId").GetString());
        Assert.NotEqual(Guid.Empty, first.GetProperty("createdByUserId").GetGuid());
        Assert.Contains("operator.", first.GetProperty("createdByEmail").GetString());
        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("previousReadingId").GetGuid());
        Assert.Equal(0.25m, second.GetProperty("differenceFromPrevious").GetDecimal());
        Assert.Equal(second.GetProperty("receivedAtServer").GetDateTimeOffset(), second.GetProperty("syncedAt").GetDateTimeOffset());
        await SetUnit(machineId, null);
        using var replay = await client.PostAsJsonAsync("/api/v1/readings", Request(machineId, 200, now, secondEvent));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var body = await replay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(second.GetProperty("id").GetGuid(), body.GetProperty("id").GetGuid());
        Assert.Equal(100.5m, body.GetProperty("value").GetDecimal());
        Assert.Equal("HOURS", body.GetProperty("meterUnit").GetString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var audit = await db.AuditLogs.SingleAsync(a => a.EntityId == second.GetProperty("id").GetGuid().ToString() && a.Action == "CREATE_READING");
        using var snapshot = JsonDocument.Parse(audit.NewValue!);
        Assert.True(snapshot.RootElement.TryGetProperty("previousReadingId", out _));
        Assert.Equal(2, await db.HourMeterReadings.CountAsync());
    }

    [Fact]
    public async Task UnitChanges_DoNotReinterpretHistory_OrCompareDifferentUnits()
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient();
        var machine = await MachineId(client);
        var first = await Create(client, machine, 100, DateTimeOffset.UtcNow.AddMinutes(-1));
        await SetUnit(machine, MeterUnit.Km);
        var current = await Create(client, machine, 10000, DateTimeOffset.UtcNow);
        Assert.Equal("KM", current.GetProperty("meterUnit").GetString());
        Assert.Equal(JsonValueKind.Null, current.GetProperty("previousReadingId").ValueKind);
        var historical = await client.GetFromJsonAsync<JsonElement>($"/api/v1/readings/{first.GetProperty("id").GetGuid()}");
        Assert.Equal("HOURS", historical.GetProperty("meterUnit").GetString());
    }

    [Fact]
    public async Task Migration_PreservesLegacyRowsWithoutFabricatingHistoricalFields()
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient();
        var machine = await MachineId(client);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        await db.Database.MigrateAsync("20261016174544_AddPhotoEvidence");
        var id = Guid.NewGuid(); var clientEvent = Guid.NewGuid(); var now = DateTimeOffset.UtcNow.UtcDateTime;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO hour_meter_readings (id, machine_id, value, reading_type, status,
                captured_at_device, received_at_server, synced_at, client_event_id, created_at, updated_at)
            VALUES ({id}, {machine}, {100.25m}, 'INTERMEDIATE', 'VALIDATED', {now}, {now}, {now}, {clientEvent}, {now}, {now})
            """);
        await db.Database.MigrateAsync();
        var historical = await db.HourMeterReadings.AsNoTracking().SingleAsync(r => r.Id == id);
        Assert.Equal(100.25m, historical.Value);
        Assert.Null(historical.MeterUnit); Assert.Null(historical.CreatedByUserId);
        Assert.Null(historical.PreviousReadingId); Assert.Null(historical.DifferenceFromPrevious);
        var created = await Create(client, machine, 101, DateTimeOffset.UtcNow);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("previousReadingId").ValueKind);
    }

    [Fact]
    public async Task MissingUnitAndInactiveWorksite_AreRejected()
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient();
        var machine = await MachineId(client); await SetUnit(machine, null);
        using var unitResponse = await client.PostAsJsonAsync("/api/v1/readings", Request(machine, 100, DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unitResponse.StatusCode);
        await SetUnit(machine, MeterUnit.Hours);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var worksite = new WorkSite("Inactive", "INACTIVE-6", false, DateTimeOffset.UtcNow);
        db.WorkSites.Add(worksite); await db.SaveChangesAsync();
        var request = Request(machine, 100, DateTimeOffset.UtcNow); request["workSiteId"] = worksite.Id;
        using var inactive = await client.PostAsJsonAsync("/api/v1/readings", request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, inactive.StatusCode);
    }

    [Theory]
    [InlineData("status", "VALIDATED")]
    [InlineData("meterUnit", "KM")]
    [InlineData("createdByEmail", "forged@tests.local")]
    [InlineData("differenceFromPrevious", 1)]
    [InlineData("receivedAtServer", "2020-01-01T00:00:00Z")]
    public async Task ClientCannotSupplyServerFields(string field, object value)
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient();
        var request = Request(await MachineId(client), 100, DateTimeOffset.UtcNow); request[field] = value;
        using var response = await client.PostAsJsonAsync("/api/v1/readings", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("123.456")]
    [InlineData("10000000000")]
    [InlineData("-0.01")]
    public async Task DecimalPrecision_IsNotSilentlyRounded(string value)
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient();
        var request = Request(await MachineId(client), decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture), DateTimeOffset.UtcNow);
        using var response = await client.PostAsJsonAsync("/api/v1/readings", request);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity });
    }

    [Fact]
    public async Task MissingValue_IsNotInterpretedAsExplicitZero()
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient();
        var request = Request(await MachineId(client), 0, DateTimeOffset.UtcNow); request.Remove("value");
        using var response = await client.PostAsJsonAsync("/api/v1/readings", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Correction_IsARestrictedPendingProposal_WithoutReplacingOriginalOrOperationalLatest()
    {
        await factory.ResetDatabaseAsync(); using var admin = factory.CreateClient();
        var machine = await MachineId(admin); var original = await Create(admin, machine, 100, DateTimeOffset.UtcNow.AddMinutes(-1));
        var originalId = original.GetProperty("id").GetGuid();
        var request = Request(machine, 99, DateTimeOffset.UtcNow); request["readingType"] = "CORRECTION"; request["correctionOfReadingId"] = originalId;
        using var op = factory.CreateClientForRole(AppRoles.Operator);
        using var forbidden = await op.PostAsJsonAsync("/api/v1/readings", request);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var supervisor = factory.CreateClientForRole(AppRoles.Supervisor);
        using var created = await supervisor.PostAsJsonAsync("/api/v1/readings", request); created.EnsureSuccessStatusCode();
        var correction = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PENDING", correction.GetProperty("status").GetString());
        Assert.Equal(originalId, correction.GetProperty("correctionOfReadingId").GetGuid());
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/readings/{originalId}");
        Assert.Equal("VALIDATED", detail.GetProperty("status").GetString());
        Assert.Equal(100m, detail.GetProperty("value").GetDecimal());
        Assert.Equal(1, detail.GetProperty("correctionCount").GetInt32());
        Assert.Single(detail.GetProperty("corrections").EnumerateArray());
        var machineDetail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/machines/{machine}");
        Assert.Equal(originalId, machineDetail.GetProperty("latestReading").GetProperty("id").GetGuid());
        request["clientEventId"] = Guid.NewGuid(); request["correctionOfReadingId"] = correction.GetProperty("id").GetGuid();
        using var chain = await supervisor.PostAsJsonAsync("/api/v1/readings", request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, chain.StatusCode);
    }

    [Fact]
    public async Task DetailAndSqlFilters_ExposeSummariesWithoutTriggeringOcr()
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient();
        var machine = await MachineId(client); var now = DateTimeOffset.UtcNow;
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var worksite = new WorkSite("Phase 6", "PHASE6", true, now); db.WorkSites.Add(worksite); await db.SaveChangesAsync();
        var first = await Create(client, machine, 100, now.AddMinutes(-2), type: "OPENING", worksite: worksite.Id);
        var second = await Create(client, machine, 90, now.AddMinutes(-1), type: "MAINTENANCE");
        using var form = new MultipartFormDataContent();
        var image = new ByteArrayContent([0xff,0xd8,0xff,0xc0,0,0x11,8,0,1,0,2,3,1,0x11,0,2,0x11,0,3,0x11,0,0xff,0xd9]);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg"); form.Add(image, "file", "photo.jpg");
        using var upload = await client.PostAsync($"/api/v1/readings/{first.GetProperty("id").GetGuid()}/photo", form); upload.EnsureSuccessStatusCode();
        var capture = Uri.EscapeDataString(now.AddMinutes(-2).ToString("O"));
        foreach (var filter in new[] { "meterUnit=HOURS&hasPhotoEvidence=true", "readingType=MAINTENANCE", "hasAnomaly=true", "hasPhotoEvidence=false",
            $"workSiteId={worksite.Id}", "status=VALIDATED", $"from={capture}&to={capture}", $"fleetNumber={first.GetProperty("machineFleetNumber").GetInt32()}&hasPhotoEvidence=true" })
        {
            var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/readings?machineId={machine}&{filter}");
            Assert.Equal(1, page.GetProperty("totalItems").GetInt32());
            Assert.Equal(JsonValueKind.Null, page.GetProperty("items")[0].GetProperty("anomalies").ValueKind);
        }
        var photoDetail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/readings/{first.GetProperty("id").GetGuid()}");
        Assert.Equal("SUCCEEDED", photoDetail.GetProperty("ocr").GetProperty("status").GetString());
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/readings/{second.GetProperty("id").GetGuid()}");
        Assert.Equal(1, detail.GetProperty("anomalyCount").GetInt32());
        Assert.Equal(1, detail.GetProperty("openAnomalyCount").GetInt32());
        Assert.Equal("READING_DECREASE", detail.GetProperty("anomalies")[0].GetProperty("type").GetString());
        Assert.Equal(1, factory.Services.GetRequiredService<FakePhotoOcrProvider>().Calls);
    }

    [Fact]
    public async Task OcrReprocessing_UsesHistoricalUnitAfterMachineUnitChanges()
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient();
        var machine = await MachineId(client); var reading = await Create(client, machine, 100, DateTimeOffset.UtcNow);
        var id = reading.GetProperty("id").GetGuid();
        using var form = new MultipartFormDataContent();
        var image = new ByteArrayContent([0xff,0xd8,0xff,0xc0,0,0x11,8,0,1,0,2,3,1,0x11,0,2,0x11,0,3,0x11,0,0xff,0xd9]);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg"); form.Add(image, "file", "photo.jpg");
        using var upload = await client.PostAsync($"/api/v1/readings/{id}/photo", form); upload.EnsureSuccessStatusCode();
        await SetUnit(machine, MeterUnit.Km); await Task.Delay(1100);
        using var reprocess = await client.PostAsync($"/api/v1/readings/{id}/photo/ocr/reprocess", null); reprocess.EnsureSuccessStatusCode();
        var result = await reprocess.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SUCCEEDED", result.GetProperty("status").GetString());
        Assert.Equal(100m, result.GetProperty("detectedValue").GetDecimal());
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/readings/{id}");
        Assert.Equal("HOURS", detail.GetProperty("meterUnit").GetString());
        Assert.Equal(100m, detail.GetProperty("ocr").GetProperty("detectedValue").GetDecimal());
    }

    [Fact]
    public async Task Pagination_IsStableAtEqualCaptureAndReceiveTimes()
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient();
        var machine = await MachineId(client); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>(); var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 27; i++) db.HourMeterReadings.Add(new HourMeterReading(machine, null, i,
            ReadingType.Intermediate, ReadingStatus.Validated, now, now, now, Guid.NewGuid(), MeterUnit.Hours));
        await db.SaveChangesAsync();
        var expected = await db.HourMeterReadings.OrderByDescending(r => r.CapturedAtDevice).ThenByDescending(r => r.ReceivedAtServer)
            .ThenByDescending(r => r.Id).Select(r => r.Id).ToArrayAsync();
        var received = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var body = await client.GetFromJsonAsync<JsonElement>($"/api/v1/readings?page={page}&pageSize=10");
            received.AddRange(body.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()));
        }
        Assert.Equal(expected, received);
        Assert.Equal(27, received.Distinct().Count());
    }

    [Fact]
    public async Task ReadingAnomalyAndAudit_RollBackTogetherWhenAuditInsertFails()
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient();
        var machine = await MachineId(client); await Create(client, machine, 100, DateTimeOffset.UtcNow.AddMinutes(-1));
        using var failingFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuditLogRepository>(); services.AddScoped<IAuditLogRepository, InvalidAuditRepository>();
        }));
        using var failingClient = failingFactory.CreateClient(); failingClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        using var response = await failingClient.PostAsJsonAsync("/api/v1/readings", Request(machine, 90, DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.Equal(1, await db.HourMeterReadings.CountAsync()); Assert.Equal(0, await db.Anomalies.CountAsync());
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "CREATE_READING"));
    }

    [Fact]
    public async Task LateSync_ConfiguredThresholdCreatesAuxiliaryAnomaly()
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient();
        var machine = await MachineId(client);
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton(new HourMeterRulesOptions { LateSyncThresholdSeconds = 3600 })));
        using var lateClient = configured.CreateClient(); lateClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        var reading = await Create(lateClient, machine, 100, DateTimeOffset.UtcNow.AddHours(-2));
        Assert.Contains(reading.GetProperty("anomalies").EnumerateArray(), a => a.GetProperty("type").GetString() == "LATE_SYNC");
        Assert.Contains(reading.GetProperty("anomalies").EnumerateArray(), a => a.GetProperty("type").GetString() == "CLOCK_SKEW");
    }

    public sealed class InvalidAuditRepository(TransjapDbContext db) : IAuditLogRepository
    {
        public void Add(AuditLog a) => db.AuditLogs.Add(new AuditLog(new string('x', 1000), a.Entity, a.EntityId, null, a.NewValue, a.CreatedAt, a.CorrelationId));
    }

    private async Task SetUnit(Guid machineId, MeterUnit? unit)
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var machine = await db.Machines.SingleAsync(m => m.Id == machineId);
        machine.Update(machine.FleetNumber, machine.Model, machine.Status, unit, DateTimeOffset.UtcNow); await db.SaveChangesAsync();
    }
    private static Dictionary<string, object?> Request(Guid machine, decimal value, DateTimeOffset captured, Guid? eventId = null) => new()
    { ["machineId"] = machine, ["value"] = value, ["capturedAtDevice"] = captured, ["readingType"] = "INTERMEDIATE", ["clientEventId"] = eventId ?? Guid.NewGuid() };
    private static async Task<JsonElement> Create(HttpClient client, Guid machine, decimal value, DateTimeOffset captured,
        Guid? eventId = null, string type = "INTERMEDIATE", string? device = null, Guid? worksite = null)
    {
        var request = Request(machine, value, captured, eventId); request["readingType"] = type; request["deviceId"] = device;
        if (worksite.HasValue) request["workSiteId"] = worksite.Value;
        using var response = await client.PostAsJsonAsync("/api/v1/readings", request); response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task<Guid> MachineId(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/v1/machines?pageSize=1")).GetProperty("items")[0].GetProperty("id").GetGuid();
}
