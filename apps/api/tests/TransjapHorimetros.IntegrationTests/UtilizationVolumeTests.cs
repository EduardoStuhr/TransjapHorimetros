using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using TransjapHorimetros.Application.Dashboard;
using TransjapHorimetros.Application.Readings;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransjapHorimetros.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace TransjapHorimetros.IntegrationTests;

public sealed class UtilizationVolumeTests(TransjapApiFactory factory, ITestOutputHelper output)
    : IClassFixture<TransjapApiFactory>
{
    [Fact]
    public async Task DashboardUnderConcurrentReadsHonoursVolumeBoundary()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransjapDbContext>();
        var machineId = await db.Machines.Select(m => m.Id).FirstAsync();
        var origin = DateTimeOffset.UtcNow.AddDays(-1);
        var samples = new ConcurrentDictionary<string, ConcurrentBag<double>>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "TransjapHorimetros.Operations")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
            samples.GetOrAdd(instrument.Name, _ => new()).Add(value));
        listener.Start();
        var previous = 0;
        foreach (var count in new[] { 100, 1000, 5000, 10000, 10001 })
        {
            var increment = count - previous;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                WITH seq AS (
                    SELECT TOP ({increment}) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int) + {previous} AS n
                    FROM sys.all_objects a CROSS JOIN sys.all_objects b
                )
                INSERT INTO hour_meter_readings
                    (id,machine_id,value,reading_type,status,captured_at_device,received_at_server,
                     synced_at,client_event_id,meter_unit,created_at,updated_at)
                SELECT NEWID(),{machineId},n * 0.01,'INTERMEDIATE','VALIDATED',DATEADD(second,n,{origin}),
                    {origin},{origin},NEWID(),'HOURS',{origin},{origin} FROM seq
                """);
            previous = count;
            var route = $"/api/v1/operations/utilization?machineId={machineId}&pageSize=5&page=2";
            if (count > 10000)
            {
                foreach (var suffix in new[] { "", "&export=true" })
                {
                    var rejected = await client.GetAsync(route + suffix);
                    Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
                    Assert.Contains("10.000", await rejected.Content.ReadAsStringAsync());
                }
                continue;
            }

            // Thirty dashboard visits in waves of three, with one separate full export.
            samples.Clear();
            var times = new List<double>();
            for (var wave = 0; wave < 10; wave++)
            {
                times.AddRange(await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
                {
                    var watch = Stopwatch.StartNew();
                    var report = await client.GetFromJsonAsync<JsonElement>(route);
                    Assert.Equal(count, report.GetProperty("totalItems").GetInt32());
                    Assert.Equal((count - 1) * 0.01m, report.GetProperty("hours").GetDecimal());
                    Assert.Equal(5, report.GetProperty("items").GetArrayLength());
                    return watch.Elapsed.TotalMilliseconds;
                })));
            }
            times.Sort();
            var queryP95 = samples["utilization.query.duration"].Order().ElementAt(28);
            var calcP95 = samples["utilization.calculation.duration"].Order().ElementAt(28);
            var full = await client.GetFromJsonAsync<JsonElement>(route + "&export=true");
            Assert.Equal(count, full.GetProperty("items").GetArrayLength());
            var message = $"LOAD rows={count} requests=30 concurrency=3 p50_ms={times[14]:F0} p95_ms={times[28]:F0} query_p95_ms={queryP95:F1} calc_p95_ms={calcP95:F1} working_set_MiB={Environment.WorkingSet / 1048576} errors=0";
            output.WriteLine(message);
            var summary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
            if (!string.IsNullOrEmpty(summary)) await File.AppendAllTextAsync(summary, message + Environment.NewLine);
            Assert.True(times[28] < 10000, message);
            if (count == 10000)
            {
                var service = scope.ServiceProvider.GetRequiredService<IOperationalService>();
                using var canceled = new CancellationTokenSource();
                canceled.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    service.GetUtilizationAsync(new ReadingQuery { MachineId = machineId }, canceled.Token));
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(route)).StatusCode);
            }
        }
    }
}
