using System.Threading.Tasks;
using KTransport.API.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace KTransport.API.Tests.Middleware;

/// <summary>
/// Behavioural tests for TASK-010's <see cref="CorrelationIdMiddleware"/>. Covers the
/// generate/echo/reuse paths without needing a real HTTP stack.
/// </summary>
public class CorrelationIdMiddlewareTests
{
    private static async Task<HttpContext> InvokeAsync(HttpContext ctx)
    {
        // Verifies the id-resolution path via HttpContext.Items — the OnStarting
        // callback that echoes the id in the response header only fires against a
        // real Kestrel response, so we assert on Items (which every downstream
        // middleware and controller consumes) instead.
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(ctx);
        return ctx;
    }

    [Fact]
    public async Task GeneratesAnId_WhenTheClientDidNotProvideOne()
    {
        var ctx = new DefaultHttpContext();

        await InvokeAsync(ctx);

        var stored = ctx.Items[CorrelationIdMiddleware.ItemKey] as string;
        Assert.False(string.IsNullOrWhiteSpace(stored));
        Assert.Matches("^[0-9a-f]{12}$", stored);
    }

    [Fact]
    public async Task ReusesTheIncomingId_WhenTheClientSuppliedOne()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Request-Id"] = "client-provided-abc123";

        await InvokeAsync(ctx);

        Assert.Equal("client-provided-abc123", ctx.Items[CorrelationIdMiddleware.ItemKey]);
    }

    [Fact]
    public async Task TruncatesRidiculouslyLongIncomingIds()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Request-Id"] = new string('x', 500);

        await InvokeAsync(ctx);

        var stored = ctx.Items[CorrelationIdMiddleware.ItemKey] as string;
        Assert.NotNull(stored);
        Assert.Equal(128, stored!.Length);
    }

    [Fact]
    public async Task GeneratesAnId_WhenIncomingHeaderIsWhitespace()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Request-Id"] = "   ";

        await InvokeAsync(ctx);

        var stored = ctx.Items[CorrelationIdMiddleware.ItemKey] as string;
        Assert.Matches("^[0-9a-f]{12}$", stored);
    }
}
