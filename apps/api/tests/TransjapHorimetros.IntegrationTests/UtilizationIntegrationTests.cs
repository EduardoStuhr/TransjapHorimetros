using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TransjapHorimetros.Domain.Security;

namespace TransjapHorimetros.IntegrationTests;

public sealed class UtilizationIntegrationTests(TransjapApiFactory factory) : IClassFixture<TransjapApiFactory>
{
    [Fact]
    public async Task UtilizationIsProtectedAndPreservesEmptyStateAndPeriodValidation()
    {
        await factory.ResetDatabaseAsync();
        using var supervisor = factory.CreateClient();
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
