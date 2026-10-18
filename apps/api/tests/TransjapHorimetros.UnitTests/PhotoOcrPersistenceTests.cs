using Microsoft.EntityFrameworkCore;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Infrastructure.Persistence;

namespace TransjapHorimetros.UnitTests;

public sealed class PhotoOcrPersistenceTests
{
    private static TransjapDbContext Context() => new(new DbContextOptionsBuilder<TransjapDbContext>()
        .UseSqlServer("Server=127.0.0.1;Database=unused_model_tests;Integrated Security=true;Encrypt=false").Options);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TerminalResult_RejectsUpdateAndDeleteBeforeDatabaseAccess(bool delete)
    {
        using var db = Context();
        var result = new PhotoOcrResult(Guid.NewGuid(), "Fake", true, DateTimeOffset.UtcNow, "test", null);
        result.Complete(PhotoOcrStatus.Failed, null, null, null, null, null, null, null, DateTimeOffset.UtcNow, 10, "OCR_TIMEOUT");
        db.Entry(result).State = delete ? EntityState.Deleted : EntityState.Modified;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void OcrModel_HasRestrictedRelationshipsAndUniqueDeduplicationIndexes()
    {
        using var db = Context();
        var result = db.Model.FindEntityType(typeof(PhotoOcrResult))!;
        Assert.All(result.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.Contains(result.GetIndexes(), ix => ix.IsUnique && ix.GetFilter() == "[IsInitial] = 1");
        Assert.Equal(4000, result.FindProperty(nameof(PhotoOcrResult.RawText))!.GetMaxLength());
        Assert.Contains(db.Model.FindEntityType(typeof(Anomaly))!.GetIndexes(), ix => ix.IsUnique
            && ix.Properties.Any(p => p.Name == nameof(Anomaly.OcrResultId)));
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
