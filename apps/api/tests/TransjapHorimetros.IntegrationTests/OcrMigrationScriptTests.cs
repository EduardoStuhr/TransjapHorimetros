using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using TransjapHorimetros.Infrastructure.Persistence;

namespace TransjapHorimetros.IntegrationTests;

public sealed class OcrMigrationScriptTests(TransjapApiFactory factory) : IClassFixture<TransjapApiFactory>
{
    private const string Previous = "20261111170305_AddReadingReviews";
    private const string Target = "20261112180316_AddOcrQualityMetadata";
    private const string IndexSql = "CREATE UNIQUE INDEX [ux_photo_ocr_request] ON [photo_ocr_results] ([RequestId]) WHERE [RequestId] IS NOT NULL;";
    private const string DeferredIndex = "EXEC sys.sp_executesql N'" + IndexSql + "';";
    private const string Data = "SELECT * FROM dbo.photo_ocr_results ORDER BY Id FOR JSON PATH";

    [Fact]
    public async Task IncrementalScriptReproduces207RollsBackAndPreservesOriginalResult()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        await OcrQualityIntegrationTests.Upload(client);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Previous);
        try
        {
            var generated = migrator.GenerateScript(Previous, Target).ReplaceLineEndings("\n").TrimEnd();
            Assert.EndsWith("\nGO", generated);
            var script = "SET XACT_ABORT ON;\n" + generated[..^3].TrimEnd();
            var releaseFile = Path.Combine(AppContext.BaseDirectory, "fase-13-ocr.sql");
            Assert.Equal(script, (await File.ReadAllTextAsync(releaseFile)).ReplaceLineEndings("\n").Trim());
            Assert.Contains(DeferredIndex, script);

            await using var connection = new SqlConnection(db.Database.GetConnectionString());
            await connection.OpenAsync();
            async Task<object?> Scalar(string sql)
            {
                await using var command = new SqlCommand(sql, connection);
                return await command.ExecuteScalarAsync();
            }

            Assert.Equal(1, await Scalar("SELECT COUNT(*) FROM dbo.photo_ocr_results"));
            var original = Assert.IsType<string>(await Scalar(Data));
            var oldColumns = (int)(await Scalar("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.photo_ocr_results')"))!;
            async Task CheckRollback()
            {
                Assert.Equal(0, await Scalar("SELECT @@TRANCOUNT"));
                Assert.Equal(oldColumns, await Scalar("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.photo_ocr_results')"));
                Assert.Equal(0, await Scalar("SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.photo_ocr_results') AND name='ux_photo_ocr_request'"));
                Assert.DoesNotContain(Target, await db.Database.GetAppliedMigrationsAsync());
                Assert.Equal(original, await Scalar(Data));
            }

            var originalError = await Assert.ThrowsAsync<SqlException>(() => Scalar(script.Replace(DeferredIndex, IndexSql)));
            Assert.Equal(207, originalError.Number);
            Assert.Contains("RequestId", originalError.Message);
            await CheckRollback();

            var injected = script.Replace("COMMIT;", "THROW 51013, 'Synthetic failure before commit', 1;");
            var rollbackError = await Assert.ThrowsAsync<SqlException>(() => Scalar(injected));
            Assert.Equal(51013, rollbackError.Number);
            await CheckRollback();

            await Scalar(script);
            Assert.Contains(Target, await db.Database.GetAppliedMigrationsAsync());
            Assert.Equal(0, await Scalar("SELECT @@TRANCOUNT"));
            Assert.Equal(oldColumns + 6, await Scalar("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.photo_ocr_results')"));
            Assert.Equal(1, await Scalar("""
                SELECT COUNT(*) FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
                WHERE i.object_id=OBJECT_ID('dbo.photo_ocr_results') AND i.name='ux_photo_ocr_request'
                AND i.is_unique=1 AND i.is_disabled=0 AND ic.key_ordinal=1
                AND COL_NAME(ic.object_id,ic.column_id)='RequestId' AND i.filter_definition='([RequestId] IS NOT NULL)'
                """));
            // FOR JSON omits new NULL fields while retaining all legacy values.
            Assert.Equal(original, await Scalar(Data));
            Assert.Single(await db.PhotoOcrResults.AsNoTracking().ToListAsync());
        }
        finally
        {
            await migrator.MigrateAsync(Target);
        }
    }
}
