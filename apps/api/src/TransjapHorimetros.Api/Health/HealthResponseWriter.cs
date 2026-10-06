using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TransjapHorimetros.Api.Health;

public static class HealthResponseWriter
{
    public static HealthCheckOptions Options(
        Func<HealthCheckRegistration, bool>? predicate = null) =>
        new()
        {
            Predicate = predicate,
            ResponseWriter = WriteResponseAsync,
        };

    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString().ToUpperInvariant(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString().ToUpperInvariant(),
                    description = entry.Value.Description,
                    durationMs = entry.Value.Duration.TotalMilliseconds,
                }),
            traceId = context.TraceIdentifier,
        });
    }
}
