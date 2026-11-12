using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Routing;

namespace TransjapHorimetros.Api.Infrastructure;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    private static readonly Meter Meter = new("TransjapHorimetros.Api");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("transjap.http.requests");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("transjap.http.duration", "ms");

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            var tags = new TagList { { "route", route }, { "method", context.Request.Method },
                { "status", context.Response.StatusCode } };
            Requests.Add(1, tags);
            Duration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
            var level = context.Response.StatusCode >= 500 ? LogLevel.Error
                : context.Response.StatusCode >= 400 ? LogLevel.Warning
                : LogLevel.Information;
            logger.Log(
                level,
                "HTTP {Method} {Path} respondeu {StatusCode} em {ElapsedMilliseconds} ms.",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
