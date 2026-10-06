using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TransjapHorimetros.Infrastructure.Persistence;

namespace TransjapHorimetros.Api.Health;

public sealed class PostgresHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL disponível.")
                : HealthCheckResult.Unhealthy("PostgreSQL indisponível.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Falha ao consultar PostgreSQL.", exception);
        }
    }
}
