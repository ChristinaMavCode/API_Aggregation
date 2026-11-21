using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace API_Aggregation.Application.Middleware;

/// <summary>
/// Pushes a CorrelationId into the Serilog LogContext for every request so that
/// all log messages within the same HTTP call share the same correlation identifier.
/// The ID is taken from the incoming X-Correlation-ID header when present (useful
/// for tracing across services) or falls back to ASP.NET's TraceIdentifier.
/// It is also written back to the response so callers can correlate on their end.
/// </summary>
public class LogContextMiddleware
{
    private readonly RequestDelegate _next;

    public LogContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                         ?? context.TraceIdentifier;

        context.Response.Headers["X-Correlation-ID"] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}