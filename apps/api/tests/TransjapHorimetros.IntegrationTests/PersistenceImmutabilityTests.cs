using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Infrastructure.Persistence;

namespace TransjapHorimetros.IntegrationTests;

public sealed class PersistenceImmutabilityTests
{
    [Fact]
    public void MachineMeterUnit_IsNullableAndStoredAsString()
    {
        using var dbContext = CreateDbContext();
        var property = dbContext.Model
            .FindEntityType(typeof(Machine))!
            .FindProperty(nameof(Machine.MeterUnit))!;

        Assert.True(property.IsNullable);
        Assert.Equal(typeof(string), property.GetValueConverter()?.ProviderClrType);
        Assert.Equal(
            "meter_unit",
            property.GetColumnName(StoreObjectIdentifier.Table("machines", null)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reading_RejectsUpdateAndDelete(bool delete)
    {
        using var dbContext = CreateDbContext();
        var reading = new HourMeterReading(
            Guid.NewGuid(),
            null,
            1m,
            ReadingType.Opening,
            ReadingStatus.Validated,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            Guid.NewGuid());
        dbContext.Entry(reading).State = delete ? EntityState.Deleted : EntityState.Modified;

        if (delete)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dbContext.SaveChangesAsync());
            Assert.Throws<InvalidOperationException>(() => dbContext.SaveChanges());
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => dbContext.SaveChanges());
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dbContext.SaveChangesAsync());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AuditLog_RejectsUpdateAndDelete(bool delete)
    {
        using var dbContext = CreateDbContext();
        var auditLog = new AuditLog(
            "TEST",
            nameof(Machine),
            Guid.NewGuid().ToString(),
            null,
            "{}",
            DateTimeOffset.UtcNow,
            "test-correlation");
        dbContext.Entry(auditLog).State = delete ? EntityState.Deleted : EntityState.Modified;

        if (delete)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dbContext.SaveChangesAsync());
            Assert.Throws<InvalidOperationException>(() => dbContext.SaveChanges());
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => dbContext.SaveChanges());
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dbContext.SaveChangesAsync());
        }
    }

    private static TransjapDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<TransjapDbContext>()
            .UseSqlServer("Server=127.0.0.1;Database=not_used;User Id=test;Password=test;Encrypt=False")
            .Options);
}
