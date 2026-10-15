using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Infrastructure.Persistence;
using TransjapHorimetros.Infrastructure.Persistence.Migrations;

namespace TransjapHorimetros.IntegrationTests;

public sealed class MachineQrCodeIntegrationTests(TransjapApiFactory factory)
    : IClassFixture<TransjapApiFactory>
{
    [Theory]
    [InlineData("68")]
    [InlineData("FROTA 68")]
    [InlineData("frota: 68")]
    public async Task Resolve_LegacyFleetFormats_ReturnFleet68(string value)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/qr-codes/resolve?value={Uri.EscapeDataString(value)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal(68, body.RootElement.GetProperty("fleetNumber").GetInt32());
        Assert.Equal(
            "LEGACY_FLEET_NUMBER",
            body.RootElement.GetProperty("resolutionSource").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("qrCodeId").ValueKind);
    }

    [Theory]
    [InlineData("0068", 68)]
    [InlineData("0070", 70)]
    [InlineData("0112", 112)]
    [InlineData("0294", 294)]
    public async Task Resolve_PhysicalFleetQrWithoutRegisteredToken_UsesSafeParser(
        string value,
        int expectedFleetNumber)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/qr-codes/resolve?value={value}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal(expectedFleetNumber, body.RootElement.GetProperty("fleetNumber").GetInt32());
        Assert.Equal(
            "LEGACY_FLEET_NUMBER",
            body.RootElement.GetProperty("resolutionSource").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("qrCodeId").ValueKind);
    }

    [Theory]
    [InlineData("0000")]
    [InlineData("68A0")]
    [InlineData("00680")]
    [InlineData("equipamento 0068")]
    [InlineData("qr da máquina 0068")]
    [InlineData("http://site/0068")]
    public async Task Resolve_InvalidPhysicalFleetQr_DoesNotResolve(string value)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/qr-codes/resolve?value={Uri.EscapeDataString(value)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Resolve_AfterRegisteringPhysicalToken_ReturnsRegisteredQr()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machineId = await GetMachineIdAsync(client, 68);
        using var issueResponse = await IssueAsync(client, machineId, "0068");
        using var issued = await ReadJsonAsync(issueResponse);

        using var response = await client.GetAsync("/api/v1/qr-codes/resolve?value=0068");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal(68, body.RootElement.GetProperty("fleetNumber").GetInt32());
        Assert.Equal("REGISTERED_QR", body.RootElement.GetProperty("resolutionSource").GetString());
        Assert.Equal(
            issued.RootElement.GetProperty("id").GetGuid(),
            body.RootElement.GetProperty("qrCodeId").GetGuid());
    }

    [Theory]
    [InlineData("QR inexistente")]
    [InlineData("equipamento de teste 68 obra 123")]
    [InlineData("https://example.com/machines/68")]
    public async Task Resolve_UnknownOrUnsafeLegacyValue_ReturnsNotFound(string value)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/qr-codes/resolve?value={Uri.EscapeDataString(value)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Resolve_EmptyValue_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/qr-codes/resolve?value=%20%20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IssueAndResolve_RegisteredQr_ReturnsCreatedAndMachine()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machineId = await GetMachineIdAsync(client, 68);

        using var issueResponse = await client.PostAsJsonAsync(
            $"/api/v1/machines/{machineId}/qr-codes",
            new { token = "  TJ-FLEET-68  " });
        Assert.Equal(HttpStatusCode.Created, issueResponse.StatusCode);
        using var issued = await ReadJsonAsync(issueResponse);
        Assert.Equal("TJ-FLEET-68", issued.RootElement.GetProperty("token").GetString());
        Assert.True(issued.RootElement.GetProperty("isActive").GetBoolean());

        using var resolveResponse = await client.GetAsync(
            "/api/v1/qr-codes/resolve?value=TJ-FLEET-68");
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
        using var resolved = await ReadJsonAsync(resolveResponse);
        Assert.Equal(machineId, resolved.RootElement.GetProperty("machineId").GetGuid());
        Assert.Equal(68, resolved.RootElement.GetProperty("fleetNumber").GetInt32());
        Assert.Equal("REGISTERED_QR", resolved.RootElement.GetProperty("resolutionSource").GetString());
        Assert.Equal(issued.RootElement.GetProperty("id").GetGuid(), resolved.RootElement.GetProperty("qrCodeId").GetGuid());
    }

    [Fact]
    public async Task Issue_WithoutToken_GeneratesCryptographicallyRandomShapedToken()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machineId = await GetMachineIdAsync(client, 68);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/machines/{machineId}/qr-codes",
            new { token = (string?)null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        var token = body.RootElement.GetProperty("token").GetString();
        Assert.NotNull(token);
        Assert.StartsWith("TJ-", token);
        Assert.Equal(35, token.Length);
    }

    [Fact]
    public async Task IssueSecondQr_DeactivatesFirstAndHistoryContainsBothNewestFirst()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machineId = await GetMachineIdAsync(client, 68);

        using var firstResponse = await IssueAsync(client, machineId, "TJ-FIRST");
        using var first = await ReadJsonAsync(firstResponse);
        var firstId = first.RootElement.GetProperty("id").GetGuid();
        using var secondResponse = await IssueAsync(client, machineId, "TJ-SECOND");
        using var second = await ReadJsonAsync(secondResponse);
        var secondId = second.RootElement.GetProperty("id").GetGuid();

        using var historyResponse = await client.GetAsync($"/api/v1/machines/{machineId}/qr-codes");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        using var history = await ReadJsonAsync(historyResponse);
        Assert.Equal(2, history.RootElement.GetArrayLength());
        Assert.Equal(secondId, history.RootElement[0].GetProperty("id").GetGuid());
        Assert.True(history.RootElement[0].GetProperty("isActive").GetBoolean());
        Assert.Equal(firstId, history.RootElement[1].GetProperty("id").GetGuid());
        Assert.False(history.RootElement[1].GetProperty("isActive").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, history.RootElement[1].GetProperty("deactivatedAt").ValueKind);
    }

    [Fact]
    public async Task ReuseDeactivatedToken_ReturnsConflict()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machineId = await GetMachineIdAsync(client, 68);
        using var first = await IssueAsync(client, machineId, "TJ-RESERVED");
        using var second = await IssueAsync(client, machineId, "TJ-CURRENT");

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/machines/{machineId}/qr-codes",
            new { token = "TJ-RESERVED" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ReuseTokenOnAnotherMachine_ReturnsConflict()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var firstMachineId = await GetMachineIdAsync(client, 68);
        var secondMachineId = await GetMachineIdAsync(client, 70);
        using var issued = await IssueAsync(client, firstMachineId, "TJ-GLOBAL");

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/machines/{secondMachineId}/qr-codes",
            new { token = "TJ-GLOBAL" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_IsIdempotentAndInactiveTokenNoLongerResolves()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machineId = await GetMachineIdAsync(client, 68);
        using var issued = await IssueAsync(client, machineId, "TJ-DEACTIVATE");

        using var firstDelete = await client.DeleteAsync(
            $"/api/v1/machines/{machineId}/qr-codes/active");
        using var secondDelete = await client.DeleteAsync(
            $"/api/v1/machines/{machineId}/qr-codes/active");
        using var resolve = await client.GetAsync(
            "/api/v1/qr-codes/resolve?value=TJ-DEACTIVATE");

        Assert.Equal(HttpStatusCode.NoContent, firstDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, secondDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, resolve.StatusCode);
    }

    [Fact]
    public async Task OperationsForMissingMachine_ReturnNotFound()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var missingId = Guid.NewGuid();

        using var issue = await client.PostAsJsonAsync(
            $"/api/v1/machines/{missingId}/qr-codes",
            new { token = "TJ-MISSING" });
        using var history = await client.GetAsync($"/api/v1/machines/{missingId}/qr-codes");
        using var deactivate = await client.DeleteAsync(
            $"/api/v1/machines/{missingId}/qr-codes/active");

        Assert.Equal(HttpStatusCode.NotFound, issue.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, history.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deactivate.StatusCode);
    }

    [Fact]
    public async Task DatabaseConstraint_AllowsOnlyOneActiveQrPerMachine()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machineId = await GetMachineIdAsync(client, 68);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        dbContext.MachineQrCodes.Add(new MachineQrCode(machineId, "TJ-DIRECT-1", DateTimeOffset.UtcNow));
        dbContext.MachineQrCodes.Add(new MachineQrCode(machineId, "TJ-DIRECT-2", DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task MachineDetails_ReturnsActiveQrToken()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machineId = await GetMachineIdAsync(client, 68);
        using var issued = await IssueAsync(client, machineId, "TJ-DETAILS");

        using var response = await client.GetAsync($"/api/v1/machines/{machineId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("TJ-DETAILS", body.RootElement.GetProperty("qrCode").GetString());
    }

    [Fact]
    public async Task Replacement_RecordsIssueAndDeactivationAuditActions()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machineId = await GetMachineIdAsync(client, 68);
        using var first = await IssueAsync(client, machineId, "TJ-AUDIT-1");
        using var second = await IssueAsync(client, machineId, "TJ-AUDIT-2");

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var actions = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(log => log.Entity == nameof(MachineQrCode))
            .OrderBy(log => log.CreatedAt)
            .Select(log => new
            {
                log.Action,
                log.EntityId,
                log.OldValue,
                log.NewValue,
                log.CorrelationId,
            })
            .ToListAsync();

        Assert.Equal(3, actions.Count);
        Assert.Equal(2, actions.Count(log => log.Action == "ISSUE_MACHINE_QR_CODE"));
        var deactivation = Assert.Single(
            actions,
            log => log.Action == "DEACTIVATE_MACHINE_QR_CODE");
        Assert.False(string.IsNullOrWhiteSpace(deactivation.EntityId));
        Assert.False(string.IsNullOrWhiteSpace(deactivation.OldValue));
        Assert.False(string.IsNullOrWhiteSpace(deactivation.NewValue));
        Assert.False(string.IsNullOrWhiteSpace(deactivation.CorrelationId));
    }

    [Fact]
    public async Task RegisteredTokenResolution_IsExactAndCaseSensitive()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machineId = await GetMachineIdAsync(client, 68);
        using var issued = await IssueAsync(client, machineId, "TJ-CaseSensitive");

        using var response = await client.GetAsync(
            "/api/v1/qr-codes/resolve?value=TJ-CASESENSITIVE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Resolve_RegisteredQrForInactiveMachine_ReturnsInactiveMachine()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        const int fleetNumber = 9902;
        using var machineResponse = await client.PostAsJsonAsync(
            "/api/v1/machines",
            new { fleetNumber, model = "Máquina inativa", status = "INACTIVE" });
        using var machine = await ReadJsonAsync(machineResponse);
        var machineId = machine.RootElement.GetProperty("id").GetGuid();
        using var issued = await IssueAsync(client, machineId, "TJ-INACTIVE");

        using var response = await client.GetAsync("/api/v1/qr-codes/resolve?value=TJ-INACTIVE");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("INACTIVE", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PhysicalQrDataPatch_IsIdempotentAndImportsOnlyCurrentFleet()
    {
        await factory.ResetDatabaseAsync();

        await using (var firstScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = firstScope.ServiceProvider.GetRequiredService<TransjapDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync(ExistingMachineQrCodeDataPatch.Sql);
            await dbContext.Database.ExecuteSqlRawAsync(ExistingMachineQrCodeDataPatch.Sql);

            Assert.Equal(
                ExistingMachineQrCodeDataPatch.ExpectedQrCodeCount,
                await dbContext.MachineQrCodes.CountAsync());
            Assert.False(await dbContext.MachineQrCodes.AnyAsync(
                qrCode => qrCode.Machine.FleetNumber == 296));
            Assert.Equal(52, await dbContext.Machines.CountAsync());
            Assert.False(await dbContext.Machines.AnyAsync(machine => machine.FleetNumber == 16));
            Assert.False(await dbContext.Machines.AnyAsync(machine => machine.FleetNumber == 76));
            Assert.False(await dbContext.Machines.AnyAsync(machine => machine.FleetNumber == 288));
        }

        using var client = factory.CreateClient();
        foreach (var (fleet, token) in new[] { (68, "0068"), (70, "0070"), (112, "0112"), (294, "0294") })
        {
            using var resolveResponse = await client.GetAsync($"/api/v1/qr-codes/resolve?value={token}");
            Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
            using var resolveBody = await ReadJsonAsync(resolveResponse);
            Assert.Equal(fleet, resolveBody.RootElement.GetProperty("fleetNumber").GetInt32());
            Assert.Equal("REGISTERED_QR", resolveBody.RootElement.GetProperty("resolutionSource").GetString());
            Assert.NotEqual(JsonValueKind.Null, resolveBody.RootElement.GetProperty("qrCodeId").ValueKind);
        }

        var machine68Id = await GetMachineIdAsync(client, 68);
        using var history68Response = await client.GetAsync($"/api/v1/machines/{machine68Id}/qr-codes");
        Assert.Equal(HttpStatusCode.OK, history68Response.StatusCode);
        using var history68Body = await ReadJsonAsync(history68Response);
        var activeQr = Assert.Single(history68Body.RootElement.EnumerateArray());
        Assert.Equal("0068", activeQr.GetProperty("token").GetString());
        Assert.True(activeQr.GetProperty("isActive").GetBoolean());

        var machine296Id = await GetMachineIdAsync(client, 296);
        using var history296Response = await client.GetAsync($"/api/v1/machines/{machine296Id}/qr-codes");
        Assert.Equal(HttpStatusCode.OK, history296Response.StatusCode);
        using var history296Body = await ReadJsonAsync(history296Response);
        Assert.Empty(history296Body.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task PhysicalQrDataPatch_WhenMachineHasConflictingActiveQr_FailsBeforeImport()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var machineId = await GetMachineIdAsync(client, 68);
        using var issued = await IssueAsync(client, machineId, "TJ-CONFLICT");

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var exception = await Assert.ThrowsAsync<SqlException>(
            () => dbContext.Database.ExecuteSqlRawAsync(ExistingMachineQrCodeDataPatch.Sql));

        Assert.Equal(51004, exception.Number);
        Assert.Equal(1, await dbContext.MachineQrCodes.CountAsync());
        Assert.False(await dbContext.MachineQrCodes.AnyAsync(qrCode => qrCode.Token == "0068"));
    }

    private static async Task<HttpResponseMessage> IssueAsync(
        HttpClient client,
        Guid machineId,
        string token)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/machines/{machineId}/qr-codes",
            new { token });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return response;
    }

    private static async Task<Guid> GetMachineIdAsync(HttpClient client, int fleetNumber)
    {
        using var response = await client.GetAsync($"/api/v1/machines/by-fleet/{fleetNumber}");
        response.EnsureSuccessStatusCode();
        using var body = await ReadJsonAsync(response);
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.ParseAsync(response.Content.ReadAsStream());
}
