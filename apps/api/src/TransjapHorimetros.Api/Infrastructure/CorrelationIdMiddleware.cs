using System.Text.RegularExpressions;

namespace TransjapHorimetros.Api.Infrastructure;

public sealed partial class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestedId = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = requestedId is not null && ValidCorrelationId().IsMatch(requestedId)
            ? requestedId
            : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidCorrelationId();
}
