using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Domain.Security;
using TransjapHorimetros.Infrastructure.Persistence;

namespace TransjapHorimetros.IntegrationTests;

public sealed class MachineMeterUnitIntegrationTests(TransjapApiFactory factory)
    : IClassFixture<TransjapApiFactory>
{
    [Fact]
    public async Task ConfigureMeterUnit_IsRestrictedAuditedAndReturnedToOfflineCache()
    {
        await factory.ResetDatabaseAsync();
        Guid machineId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
            machineId = await dbContext.Machines.OrderBy(machine => machine.FleetNumber)
                .Select(machine => machine.Id)
                .FirstAsync();
            await dbContext.Machines.Where(machine => machine.Id == machineId)
                .ExecuteUpdateAsync(set => set.SetProperty(machine => machine.MeterUnit, (MeterUnit?)null));
        }

        using var operatorClient = factory.CreateClientForRole(AppRoles.Operator);
        using var forbidden = await operatorClient.PatchAsJsonAsync(
            $"/api/v1/machines/{machineId}/meter-unit",
            new { meterUnit = "KM" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var managerClient = factory.CreateClientForRole(AppRoles.Manager);
        using var response = await managerClient.PatchAsJsonAsync(
            $"/api/v1/machines/{machineId}/meter-unit",
            new { meterUnit = "KM" });
        response.EnsureSuccessStatusCode();
        var configured = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("KM", configured.GetProperty("meterUnit").GetString());

        using var referenceResponse = await operatorClient.GetAsync("/api/v1/offline/reference-data");
        referenceResponse.EnsureSuccessStatusCode();
        var referenceData = await referenceResponse.Content.ReadFromJsonAsync<JsonElement>();
        var cachedMachine = referenceData.GetProperty("machines").EnumerateArray()
            .Single(machine => machine.GetProperty("id").GetGuid() == machineId);
        Assert.Equal("KM", cachedMachine.GetProperty("meterUnit").GetString());

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.Equal(MeterUnit.Km, await verificationDb.Machines.Where(machine => machine.Id == machineId)
            .Select(machine => machine.MeterUnit)
            .SingleAsync());
        Assert.True(await verificationDb.Set<AuditLog>().AnyAsync(log =>
            log.Action == AuditActions.MachineUpdated
            && log.Entity == nameof(Machine)
            && log.EntityId == machineId.ToString()));
    }

    [Fact]
    public async Task ConfigureMeterUnit_RejectsNullWithoutChangingOfficialMachine()
    {
        await factory.ResetDatabaseAsync();
        Guid machineId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
            machineId = await dbContext.Machines.Select(machine => machine.Id).FirstAsync();
        }

        using var adminClient = factory.CreateClientForRole(AppRoles.Admin);
        using var response = await adminClient.PatchAsJsonAsync(
            $"/api/v1/machines/{machineId}/meter-unit",
            new { meterUnit = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.Equal(MeterUnit.Hours, await verificationDb.Machines.Where(machine => machine.Id == machineId)
            .Select(machine => machine.MeterUnit)
            .SingleAsync());
    }
}
