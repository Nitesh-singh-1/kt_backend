using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace KTransport.API.Middleware
{
    /// <summary>
    /// Assigns a stable correlation id to every request so a client, a proxy, and the
    /// server can pin logs to the same trace. The id is read from the incoming
    /// <c>X-Request-Id</c> header (so a caller — e.g. a browser fetch, a load balancer,
    /// or curl — can supply its own) and generated if absent. The id is:
    ///   - stashed on <c>HttpContext.Items["CorrelationId"]</c> for other middleware
    ///     (notably <see cref="ExceptionHandlingMiddleware"/>) and controllers,
    ///   - pushed into Serilog's <see cref="LogContext"/> for the duration of the
    ///     request so every log line emitted inside the pipeline includes it, and
    ///   - echoed back in the <c>X-Request-Id</c> response header.
    /// Must run BEFORE <see cref="ExceptionHandlingMiddleware"/> so an unhandled
    /// exception's error envelope carries the same id the client saw in the header.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private const string HeaderName = "X-Request-Id";
        public const string ItemKey = "CorrelationId";

        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = ResolveIncomingId(context) ?? GenerateId();

            context.Items[ItemKey] = correlationId;

            // Echo it back BEFORE downstream middleware writes the response, so even if
            // something errors early the client still has the id.
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(HeaderName))
                {
                    context.Response.Headers[HeaderName] = correlationId;
                }
                return Task.CompletedTask;
            });

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }

        private static string? ResolveIncomingId(HttpContext context)
        {
            if (!context.Request.Headers.TryGetValue(HeaderName, out var values)) return null;
            var value = values.ToString();
            if (string.IsNullOrWhiteSpace(value)) return null;
            // Bound the accepted length so a malicious caller can't blow up log lines.
            return value.Length > 128 ? value.Substring(0, 128) : value;
        }

        // Short-ish, url-safe, sortable-enough id. Not a real ULID — just the first 12
        // hex chars of a GUID, which gives 48 bits and is plenty for correlation.
        private static string GenerateId() => Guid.NewGuid().ToString("N").Substring(0, 12);
    }
}
