using System.Net;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using TransjapHorimetros.Infrastructure.Persistence.Repositories;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransjapHorimetros.Infrastructure.Persistence;

namespace TransjapHorimetros.IntegrationTests;

public sealed class ReliabilityIntegrationTests(TransjapApiFactory factory) : IClassFixture<TransjapApiFactory>
{
    [Theory]
    [InlineData("value")]
    [InlineData("capturedAtDevice")]
    [InlineData("deviceId")]
    [InlineData("invalidDeviceId")]
    [InlineData("readingType")]
    [InlineData("workSiteId")]
    public async Task DivergentPayloadReturnsConflictWithoutChangingHistory(string field)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var request = await Request(client);
        var first = await client.PostAsJsonAsync("/api/v1/readings", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        request[field == "invalidDeviceId" ? "deviceId" : field] = field switch
        {
            "value" => 101m,
            "invalidDeviceId" => new string('x', 101),
            "capturedAtDevice" => DateTimeOffset.UtcNow.AddDays(-2),
            "readingType" => "OPENING",
            "workSiteId" => Guid.NewGuid(),
            _ => "different-device"
        };
        var conflict = await client.PostAsJsonAsync("/api/v1/readings", request);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.Equal(100m, (await db.HourMeterReadings.SingleAsync()).Value);
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "CREATE_READING"));
    }

    [Fact]
    public async Task ParallelRequestsAndLostResponsePersistOneEventAndOneAudit()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var request = await Request(client);
        var responses = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => client.PostAsJsonAsync("/api/v1/readings", request)));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode));
        var ids = new List<Guid>();
        foreach (var response in responses)
            ids.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        Assert.Single(ids.Distinct());
        // The caller lost its first response and resends the exact persisted event.
        var replay = await client.PostAsJsonAsync("/api/v1/readings", request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.Equal(1, await db.HourMeterReadings.CountAsync());
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "CREATE_READING"));
    }

    [Fact]
    public async Task ConcurrentDifferentReadingsUseACommittedPredecessor()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var first = await Request(client);
        var second = new Dictionary<string, object>(first) { ["clientEventId"] = Guid.NewGuid() };
        // Equal values are valid, different capture times avoid the duplicate heuristic.
        second["capturedAtDevice"] = ((DateTimeOffset)first["capturedAtDevice"]).AddMinutes(1);
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/v1/readings", first),
            client.PostAsJsonAsync("/api/v1/readings", second));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        Assert.Equal(2, await db.HourMeterReadings.CountAsync());
        var ordered = await db.HourMeterReadings.OrderBy(r => r.ReceivedAtServer).ToListAsync();
        // Serialization guarantees the later transaction sees the earlier committed row when chronological.
        if (ordered[0].CapturedAtDevice < ordered[1].CapturedAtDevice)
            Assert.Equal(ordered[0].Id, ordered[1].PreviousReadingId);
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "CREATE_READING"));
    }

    [Fact]
    public async Task AuditFailureRollsBackReadingAndCanBeRetried()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var request = await Request(client);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reliability_reject_audit ON audit_logs AFTER INSERT AS
            BEGIN THROW 51019, 'Injected failure in isolated test database', 1; END
            """);
        try
        {
            var failure = await client.PostAsJsonAsync("/api/v1/readings", request);
            Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
            Assert.DoesNotContain("Injected failure", await failure.Content.ReadAsStringAsync());
            Assert.Equal(0, await db.HourMeterReadings.CountAsync());
            Assert.Equal(0, await db.AuditLogs.CountAsync(a => a.Action == "CREATE_READING"));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reliability_reject_audit");
        }
        var retry = await client.PostAsJsonAsync("/api/v1/readings", request);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(1, await db.HourMeterReadings.CountAsync());
    }

    [Fact]
    public async Task MachineLockTimeoutReturnsRetryable503AndPreservesEvent()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var request = await Request(client);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        // The test factory already guards this connection as a disposable test database.
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand();
        command.Transaction = (Microsoft.Data.SqlClient.SqlTransaction)transaction;
        command.CommandText = "EXEC sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Transaction'";
        command.Parameters.AddWithValue("@resource", $"transjap-reading-{request["machineId"]:D}");
        await command.ExecuteNonQueryAsync();
        var response = await client.PostAsJsonAsync("/api/v1/readings", request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("5", response.Headers.GetValues("Retry-After").Single());
        Assert.Equal(0, await db.HourMeterReadings.CountAsync());
        await transaction.RollbackAsync();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/readings", request)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransactionRetryPreservesOneReadingWhenCommitAcknowledgementIsLost(bool afterCommit)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var request = await Request(client);
        using var scope = factory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<TransjapDbContext>().Database.GetConnectionString()!;
        TestDatabaseGuard.EnsureSafeTestDatabase(TestDatabaseGuard.TestingEnvironmentName, connection);
        var options = new DbContextOptionsBuilder<TransjapDbContext>().UseSqlServer(connection)
            .ReplaceService<IExecutionStrategyFactory, RetryFactory>()
            .AddInterceptors(new CommitFailure(afterCommit)).Options;
        await using var db = new TransjapDbContext(options);
        var repository = new TransjapRepository(db);
        var machine = (Guid)request["machineId"];
        var eventId = (Guid)request["clientEventId"];
        var id = await repository.ExecuteForMachineAsync(machine, async () =>
        {
            var existing = await db.HourMeterReadings.SingleOrDefaultAsync(r => r.ClientEventId == eventId);
            if (existing is not null) return existing.Id;
            var now = DateTimeOffset.UtcNow;
            var reading = new TransjapHorimetros.Domain.Entities.HourMeterReading(machine, null, 100,
                TransjapHorimetros.Domain.Enums.ReadingType.Intermediate,
                TransjapHorimetros.Domain.Enums.ReadingStatus.Validated, now, now, now, eventId);
            db.HourMeterReadings.Add(reading);
            db.AuditLogs.Add(new TransjapHorimetros.Domain.Entities.AuditLog("CREATE_READING",
                "HourMeterReading", reading.Id.ToString(), null, "{}", now, "commit-recovery"));
            await repository.SaveChangesAsync(default);
            return reading.Id;
        }, default);
        Assert.Equal(1, await db.HourMeterReadings.CountAsync(r => r.ClientEventId == eventId));
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityId == id.ToString()));
    }

    public sealed class RetryFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    {
        public IExecutionStrategy Create() => new CommitRetry(dependencies);
    }

    private sealed class CommitRetry(ExecutionStrategyDependencies dependencies)
        : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is TimeoutException;
    }

    private sealed class CommitFailure(bool afterCommit) : DbTransactionInterceptor
    {
        private bool failed;
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (!afterCommit && !failed) { failed = true; throw new TimeoutException("isolated pre-commit failure"); }
            return ValueTask.FromResult(result);
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (afterCommit && !failed) { failed = true; throw new TimeoutException("isolated lost acknowledgement"); }
            return Task.CompletedTask;
        }
    }

    private static async Task<Dictionary<string, object>> Request(HttpClient client)
    {
        var machine = await client.GetFromJsonAsync<JsonElement>("/api/v1/machines/by-fleet/68");
        return new Dictionary<string, object>
        {
            ["machineId"] = machine.GetProperty("id").GetGuid(),
            ["value"] = 100m, ["readingType"] = "INTERMEDIATE",
            ["capturedAtDevice"] = DateTimeOffset.UtcNow.AddMinutes(-2),
            ["clientEventId"] = Guid.NewGuid(), ["deviceId"] = "reliability-test"
        };
    }
}
