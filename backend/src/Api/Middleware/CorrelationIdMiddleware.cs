using Serilog.Context;

namespace Api.Middleware;

/// <summary>
/// Accepted from the client if present, otherwise generated
/// (docs/api/api-conventions.md). Overwrites HttpContext.TraceIdentifier
/// rather than introducing a parallel "correlation id" concept — every
/// existing use of TraceIdentifier (AppExceptionHandler's traceId field,
/// AuditWriter's CorrelationId column) picks this up for free with no
/// changes there. Must run before UseExceptionHandler so an exception
/// thrown deeper in the pipeline is still tagged with it.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing) && !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString();

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
