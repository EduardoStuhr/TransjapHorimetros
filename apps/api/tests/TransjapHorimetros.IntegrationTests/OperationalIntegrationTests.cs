using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransjapHorimetros.Domain.Security;
using TransjapHorimetros.Application.Dashboard;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Infrastructure.Persistence;

namespace TransjapHorimetros.IntegrationTests;

public sealed class OperationalIntegrationTests(TransjapApiFactory factory) : IClassFixture<TransjapApiFactory>
{
    private sealed class ReviewActor(Guid id) : ICurrentUser
    {
        public Guid? UserId => id;
        public string? Email => "review@tests.local";
        public string? FullName => "Reviewer";
        public IReadOnlyList<string> Roles => [AppRoles.Admin];
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => role == AppRoles.Admin;
    }

    [Fact]
    public async Task OperatorCannotReviewOrReadAudit()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientForRole(AppRoles.Operator);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/operations/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            $"/api/v1/operations/readings/{Guid.NewGuid()}/review", new { approved = true, reason = "Revisado" })).StatusCode);
    }

    [Fact]
    public async Task SummaryTranslatesAndFiltersOnSqlServer()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        using (var diagnosticScope = factory.Services.CreateScope())
            await diagnosticScope.ServiceProvider.GetRequiredService<IOperationalService>().GetSummaryAsync(new(), default);
        var summary = await client.GetFromJsonAsync<JsonElement>("/api/v1/operations/summary");
        Assert.True(summary.GetProperty("totalMachines").GetInt32() > 0);
        Assert.Equal(0, summary.GetProperty("readings").GetInt32());
        var empty = await client.GetFromJsonAsync<JsonElement>("/api/v1/operations/summary?fleetNumber=99999999");
        Assert.Equal(0, empty.GetProperty("totalMachines").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/operations/summary?from=2026-11-11&to=2026-01-01")).StatusCode);
    }

    [Fact]
    public async Task ConflictingReviewsPersistOneDecisionAndAuditWithoutChangingReading()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machines = await client.GetFromJsonAsync<JsonElement>("/api/v1/machines?pageSize=1");
        var machine = machines.GetProperty("items")[0].GetProperty("id").GetGuid();
        async Task<JsonElement> Create(decimal value) {
            var result = await client.PostAsJsonAsync("/api/v1/readings", new {
                machineId = machine, value, readingType = "INTERMEDIATE", capturedAtDevice = DateTimeOffset.UtcNow,
                clientEventId = Guid.NewGuid()
            });
            result.EnsureSuccessStatusCode();
            return await result.Content.ReadFromJsonAsync<JsonElement>();
        }
        await Create(100);
        var suspect = await Create(90);
        var id = suspect.GetProperty("id").GetGuid();
        var url = $"/api/v1/operations/readings/{id}/review";
        using (var diagnosticScope = factory.Services.CreateScope())
        {
            var database = diagnosticScope.ServiceProvider.GetRequiredService<TransjapDbContext>();
            var actor = new ReviewActor(await database.Users.Select(u => u.Id).FirstAsync());
            var service = new OperationalService(database, actor, TimeProvider.System);
            await service.ReviewAsync(id, new ReviewRequest(true, "Conferida a evidência"), "integration", default);
        }
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(url, new { approved = true, reason = "Conferida a evidência" }),
            client.PostAsJsonAsync(url, new { approved = false, reason = "Solicitada nova leitura" }));
        Assert.Single(responses.Where(r => r.IsSuccessStatusCode));
        Assert.Single(responses.Where(r => r.StatusCode == HttpStatusCode.Conflict));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.Equal(1, await db.ReadingReviews.CountAsync(r => r.ReadingId == id));
        Assert.Equal(1, await db.AuditLogs.CountAsync(r => r.Action == "READING_REVIEWED"));
        var original = await db.HourMeterReadings.SingleAsync(r => r.Id == id);
        Assert.Equal(90, original.Value);
        Assert.Equal(suspect.GetProperty("status").GetString(), original.Status.ToString().ToUpperInvariant());
        var decision = await client.GetFromJsonAsync<JsonElement>(url);
        var retry = await client.PostAsJsonAsync(url, new {
            approved = decision.GetProperty("approved").GetBoolean(), reason = decision.GetProperty("reason").GetString()
        });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(1, await db.ReadingReviews.CountAsync(r => r.ReadingId == id));
    }
}
