using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TransjapHorimetros.Infrastructure.Persistence;

public sealed class TransjapDbContextFactory : IDesignTimeDbContextFactory<TransjapDbContext>
{
    public TransjapDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=127.0.0.1,1433;Database=transjap_horimetros;User Id=sa;Encrypt=False;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<TransjapDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new TransjapDbContext(options);
    }
}
