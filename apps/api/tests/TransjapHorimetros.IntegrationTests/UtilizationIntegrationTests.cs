using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TransjapHorimetros.Domain.Security;

namespace TransjapHorimetros.IntegrationTests;

public sealed class UtilizationIntegrationTests(TransjapApiFactory factory) : IClassFixture<TransjapApiFactory>
{
    [Fact]
    public async Task IntervalQueryPreservesNeighboursAndPaginatesWithoutLosingTotals()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machine = await client.GetFromJsonAsync<JsonElement>("/api/v1/machines/by-fleet/68");
        var siteResponse = await client.PostAsJsonAsync("/api/v1/worksites",
            new { name = "Obra de homologação", code = "SYN", active = true });
        siteResponse.EnsureSuccessStatusCode();
        var site = (await siteResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/readings", new {
                machineId = machine.GetProperty("id").GetGuid(), value = 100m + i * 0.11m,
                readingType = "INTERMEDIATE", capturedAtDevice = DateTimeOffset.UtcNow.AddHours(-3 + i),
                workSiteId = i == 0 ? (Guid?)null : site, clientEventId = Guid.NewGuid()
            });
            response.EnsureSuccessStatusCode();
            var reading = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (reading.GetProperty("status").GetString() is "PENDING" or "SUSPECT")
            {
                var review = await client.PostAsJsonAsync("/api/v1/operations/readings/" + reading.GetProperty("id").GetGuid() + "/review",
                    new { approved = true, reason = "Registro sintético conferido" });
                review.EnsureSuccessStatusCode();
            }
        }
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/operations/utilization?fleetNumber=68&pageSize=1&page=2");
        Assert.Equal(3, page.GetProperty("totalItems").GetInt32());
        Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(0.22m, page.GetProperty("hours").GetDecimal());
        var full = await client.GetFromJsonAsync<JsonElement>("/api/v1/operations/utilization?fleetNumber=68&export=true");
        foreach (var key in new[] { "byFleet", "bySite", "byDay", "unassignedHours", "unavailableByReason" })
            Assert.Equal(full.GetProperty(key).GetRawText(), page.GetProperty(key).GetRawText());
        Assert.Equal(0.22m, page.GetProperty("byFleet")[0].GetProperty("value").GetDecimal());
        Assert.Equal(0.11m, page.GetProperty("unassignedHours").GetDecimal());
        Assert.Equal(0.11m, page.GetProperty("bySite")[0].GetProperty("value").GetDecimal());
        var filtered = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/operations/utilization?fleetNumber=68&workSiteId={site}&meterUnit=HOURS&hasPhotoEvidence=false");
        Assert.Equal(2, filtered.GetProperty("totalItems").GetInt32());
        Assert.Equal(0.11m, filtered.GetProperty("hours").GetDecimal());
        Assert.Equal(1, filtered.GetProperty("unavailableByReason").GetProperty("SITE_UNCERTAIN").GetInt32());
        Assert.Equal(0.22m, page.GetProperty("byDay").EnumerateArray().Sum(x => x.GetProperty("value").GetDecimal()));
        Assert.Equal(3, full.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task UtilizationIsProtectedAndPreservesEmptyStateAndPeriodValidation()
    {
        await factory.ResetDatabaseAsync();
        using var supervisor = factory.CreateClientForRole(AppRoles.Supervisor);
        using var anonymous = factory.CreateAnonymousClient();
        using var op = factory.CreateClientForRole(AppRoles.Operator);
        const string route = "/api/v1/operations/utilization";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync(route)).StatusCode);
        var result = await supervisor.GetFromJsonAsync<JsonElement>(route + "?fleetNumber=999999");
        Assert.Equal(JsonValueKind.Null, result.GetProperty("hours").ValueKind);
        Assert.Empty(result.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await supervisor.GetAsync(route + "?from=2020-01-01&to=2026-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await supervisor.GetAsync(route + "?from=2026-01-02&to=2026-01-01")).StatusCode);
    }
}
