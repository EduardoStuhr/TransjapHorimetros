using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TransjapHorimetros.Domain.Security;

namespace TransjapHorimetros.IntegrationTests;

public sealed class OfflineReferenceDataIntegrationTests(TransjapApiFactory factory)
    : IClassFixture<TransjapApiFactory>
{
    [Fact]
    public async Task ReferenceData_IsAuthenticatedMinimalAndContainsActiveQrTokens()
    {
        await factory.ResetDatabaseAsync();
        using var anonymous = factory.CreateAnonymousClient();
        using var unauthorized = await anonymous.GetAsync("/api/v1/offline/reference-data");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var client = factory.CreateClientForRole(AppRoles.Operator);
        using var response = await client.GetAsync("/api/v1/offline/reference-data");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetProperty("schemaVersion").GetInt32());
        Assert.NotEqual(default, body.GetProperty("generatedAt").GetDateTimeOffset());
        var machines = body.GetProperty("machines").EnumerateArray().ToArray();
        Assert.NotEmpty(machines);
        Assert.All(machines, machine =>
        {
            Assert.Equal("ACTIVE", machine.GetProperty("status").GetString());
            Assert.True(machine.TryGetProperty("activeQrToken", out _));
            Assert.False(machine.TryGetProperty("latestReading", out _));
        });
        Assert.All(body.GetProperty("workSites").EnumerateArray(), workSite =>
            Assert.True(workSite.GetProperty("active").GetBoolean()));
    }
}
