using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TransjapHorimetros.IntegrationTests;

public sealed class ApiIntegrationTests(TransjapApiFactory factory) : IClassFixture<TransjapApiFactory>
{
    [Fact]
    public async Task MachinesEndpoints_ReturnRealSeedAndFleet68()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var machinesResponse = await client.GetAsync("/api/v1/machines?page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, machinesResponse.StatusCode);
        using var machines = await JsonDocument.ParseAsync(await machinesResponse.Content.ReadAsStreamAsync());
        Assert.Equal(52, machines.RootElement.GetProperty("totalItems").GetInt32());
        Assert.Equal(52, machines.RootElement.GetProperty("items").GetArrayLength());

        using var fleetResponse = await client.GetAsync("/api/v1/machines/by-fleet/68");
        Assert.Equal(HttpStatusCode.OK, fleetResponse.StatusCode);
        using var fleet = await JsonDocument.ParseAsync(await fleetResponse.Content.ReadAsStreamAsync());
        Assert.Equal(68, fleet.RootElement.GetProperty("fleetNumber").GetInt32());
        Assert.Equal("Pipa Ford", fleet.RootElement.GetProperty("model").GetString());
        Assert.Equal(JsonValueKind.Null, fleet.RootElement.GetProperty("qrCode").ValueKind);
    }

    [Fact]
    public async Task ReadingEndpoints_CreateListAndDeduplicateClientEvent()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machineId = await GetMachineIdAsync(client, 68);
        var clientEventId = Guid.NewGuid();
        var request = ReadingRequest(machineId, 100m, DateTimeOffset.UtcNow.AddHours(-2), clientEventId);

        using var firstResponse = await client.PostAsJsonAsync("/api/v1/readings", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        using var first = await JsonDocument.ParseAsync(await firstResponse.Content.ReadAsStreamAsync());
        Assert.Equal("VALIDATED", first.RootElement.GetProperty("status").GetString());

        using var duplicateResponse = await client.PostAsJsonAsync("/api/v1/readings", request);
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        using var duplicate = await JsonDocument.ParseAsync(await duplicateResponse.Content.ReadAsStreamAsync());
        Assert.Equal(
            first.RootElement.GetProperty("id").GetGuid(),
            duplicate.RootElement.GetProperty("id").GetGuid());

        using var readingsResponse = await client.GetAsync($"/api/v1/machines/{machineId}/readings");
        Assert.Equal(HttpStatusCode.OK, readingsResponse.StatusCode);
        using var readings = await JsonDocument.ParseAsync(await readingsResponse.Content.ReadAsStreamAsync());
        Assert.Equal(1, readings.RootElement.GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task LowerReading_IsSuspectAndCreatesReadingDecreaseAnomaly()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machineId = await GetMachineIdAsync(client, 68);
        var capturedAt = DateTimeOffset.UtcNow.AddHours(-4);

        using var first = await client.PostAsJsonAsync(
            "/api/v1/readings",
            ReadingRequest(machineId, 100m, capturedAt, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await client.PostAsJsonAsync(
            "/api/v1/readings",
            ReadingRequest(machineId, 90m, capturedAt.AddHours(1), Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        using var secondBody = await JsonDocument.ParseAsync(await second.Content.ReadAsStreamAsync());
        Assert.Equal("SUSPECT", secondBody.RootElement.GetProperty("status").GetString());

        using var anomaliesResponse = await client.GetAsync($"/api/v1/anomalies?machineId={machineId}");
        Assert.Equal(HttpStatusCode.OK, anomaliesResponse.StatusCode);
        using var anomalies = await JsonDocument.ParseAsync(await anomaliesResponse.Content.ReadAsStreamAsync());
        Assert.Equal(1, anomalies.RootElement.GetProperty("totalItems").GetInt32());
        Assert.Equal(
            "READING_DECREASE",
            anomalies.RootElement.GetProperty("items")[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task PlausibleSequence_IsValidatedWithoutAnomaly()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machineId = await GetMachineIdAsync(client, 68);
        var capturedAt = DateTimeOffset.UtcNow.AddHours(-12);

        using var first = await client.PostAsJsonAsync(
            "/api/v1/readings",
            ReadingRequest(machineId, 100m, capturedAt, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await client.PostAsJsonAsync(
            "/api/v1/readings",
            ReadingRequest(machineId, 108.5m, capturedAt.AddHours(9), Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        using var secondBody = await JsonDocument.ParseAsync(await second.Content.ReadAsStreamAsync());
        Assert.Equal("VALIDATED", secondBody.RootElement.GetProperty("status").GetString());

        using var anomaliesResponse = await client.GetAsync($"/api/v1/anomalies?machineId={machineId}");
        Assert.Equal(HttpStatusCode.OK, anomaliesResponse.StatusCode);
        using var anomalies = await JsonDocument.ParseAsync(await anomaliesResponse.Content.ReadAsStreamAsync());
        Assert.Equal(0, anomalies.RootElement.GetProperty("totalItems").GetInt32());
    }

    private static async Task<Guid> GetMachineIdAsync(HttpClient client, int fleetNumber)
    {
        using var response = await client.GetAsync($"/api/v1/machines/by-fleet/{fleetNumber}");
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static object ReadingRequest(
        Guid machineId,
        decimal value,
        DateTimeOffset capturedAt,
        Guid clientEventId) =>
        new
        {
            machineId,
            workSiteId = (Guid?)null,
            value,
            readingType = "OPENING",
            capturedAtDevice = capturedAt,
            clientEventId,
        };
}
