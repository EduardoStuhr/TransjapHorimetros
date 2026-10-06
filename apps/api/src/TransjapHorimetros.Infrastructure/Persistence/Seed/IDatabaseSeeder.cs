namespace TransjapHorimetros.Infrastructure.Persistence.Seed;

public interface IDatabaseSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
