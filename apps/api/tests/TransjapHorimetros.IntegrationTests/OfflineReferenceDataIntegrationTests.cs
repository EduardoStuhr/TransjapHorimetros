using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Security;
using TransjapHorimetros.Infrastructure.Persistence;

namespace TransjapHorimetros.IntegrationTests;

public sealed class OfflineReferenceDataIntegrationTests(TransjapApiFactory factory)
    : IClassFixture<TransjapApiFactory>
{
    [Fact]
    public async Task ReferenceData_IsAuthenticatedMinimalAndContainsActiveQrTokens()
    {
        await factory.ResetDatabaseAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
            dbContext.WorkSites.Add(new WorkSite(
                "Obra offline de teste",
                "OFFLINE-REF",
                true,
                DateTimeOffset.UtcNow));
            await dbContext.SaveChangesAsync();
        }

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
            Assert.True(machine.TryGetProperty("meterUnit", out var meterUnit) && meterUnit.ValueKind != JsonValueKind.Undefined);
            Assert.True(machine.GetProperty("fleetNumber").GetInt32() > 0);
            Assert.False(string.IsNullOrWhiteSpace(machine.GetProperty("model").GetString()));
            Assert.False(machine.TryGetProperty("latestReading", out _));
            Assert.False(machine.TryGetProperty("createdBy", out _));
            Assert.False(machine.TryGetProperty("notes", out _));
        });
        var workSites = body.GetProperty("workSites").EnumerateArray().ToArray();
        Assert.NotEmpty(workSites);
        Assert.All(workSites, workSite =>
        {
            Assert.True(workSite.GetProperty("active").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(workSite.GetProperty("name").GetString()));
            Assert.NotEqual(Guid.Empty, workSite.GetProperty("id").GetGuid());
        });
    }
}
