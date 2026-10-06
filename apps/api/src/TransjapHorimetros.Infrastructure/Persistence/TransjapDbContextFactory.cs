using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TransjapHorimetros.Infrastructure.Persistence;

public sealed class TransjapDbContextFactory : IDesignTimeDbContextFactory<TransjapDbContext>
{
    public TransjapDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=127.0.0.1;Port=5432;Database=transjap_horimetros;Username=postgres";
        var options = new DbContextOptionsBuilder<TransjapDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new TransjapDbContext(options);
    }
}
