using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace KTransport.API.Middleware
{
    /// <summary>
    /// Catches unhandled exceptions and returns a consistent JSON error envelope
    /// ({ success:false, message, traceId }) instead of a raw/HTML 500. The real exception
    /// is logged with the traceId; the client only sees a generic message (no internal leakage).
    /// Only affects the UNHANDLED-error path — success responses are untouched.
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // Prefer the CorrelationId set by CorrelationIdMiddleware so the value
                // clients see in the X-Request-Id response header matches what's in the
                // error envelope (and in the logs). Fall back to the ASP.NET-generated
                // TraceIdentifier for defence in depth if this middleware runs without
                // its upstream partner.
                var traceId = (context.Items[CorrelationIdMiddleware.ItemKey] as string)
                    ?? context.TraceIdentifier;
                _logger.LogError(ex, "Unhandled exception (traceId {TraceId}) on {Method} {Path}",
                    traceId, context.Request.Method, context.Request.Path);

                if (context.Response.HasStarted)
                {
                    // Too late to change the response; just let it surface.
                    throw;
                }

                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                var payload = JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "An unexpected error occurred. Please try again, and contact support with the reference if it persists.",
                    traceId
                });
                await context.Response.WriteAsync(payload);
            }
        }
    }
}
