using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Infrastructure.Persistence.Seed;

public sealed class DatabaseSeeder(
    TransjapDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<DatabaseSeeder> logger) : IDatabaseSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seededFleetNumbers = MachineSeedData.All.Select(machine => machine.FleetNumber).ToArray();
        var existingFleetNumbers = await dbContext.Machines
            .Where(machine => seededFleetNumbers.Contains(machine.FleetNumber))
            .Select(machine => machine.FleetNumber)
            .ToListAsync(cancellationToken);
        var missingMachines = MachineSeedData.All
            .Where(machine => !existingFleetNumbers.Contains(machine.FleetNumber))
            .Select(machine => new Machine(
                machine.FleetNumber,
                machine.Model,
                MachineStatus.Active,
                timeProvider.GetUtcNow()))
            .ToArray();

        if (missingMachines.Length == 0)
        {
            logger.LogInformation("Seed da frota já está atualizado com {MachineCount} máquinas.", MachineSeedData.All.Count);
            return;
        }

        dbContext.Machines.AddRange(missingMachines);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed adicionou {MachineCount} máquinas reais da frota.", missingMachines.Length);
    }
}
