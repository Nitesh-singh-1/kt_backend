using System.Collections.Generic;
using System.Linq;
using KTransport.API.Common;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace KTransport.API.Tests.Common;

/// <summary>
/// Unit tests for TASK-011c's <see cref="PaginationHelper"/>. Pure function over an
/// in-memory list + a DefaultHttpContext; no DB, no HTTP.
/// </summary>
public class PaginationHelperTests
{
    private static List<int> Range(int n) => Enumerable.Range(1, n).ToList();

    [Fact]
    public void ReturnsFullList_WhenPageAndPageSizeAreNull()
    {
        var all = Range(50);
        var ctx = new DefaultHttpContext();

        var result = PaginationHelper.Paginate(all, ctx, page: null, pageSize: null);

        Assert.Equal(50, result.Count);
        Assert.Equal("50", ctx.Response.Headers["X-Total-Count"]);
    }

    [Fact]
    public void ReturnsRequestedSlice_WhenPaginated()
    {
        var all = Range(50);
        var ctx = new DefaultHttpContext();

        var result = PaginationHelper.Paginate(all, ctx, page: 2, pageSize: 10);

        Assert.Equal(10, result.Count);
        Assert.Equal(11, result[0]);
        Assert.Equal(20, result[9]);
        Assert.Equal("50", ctx.Response.Headers["X-Total-Count"]);
    }

    [Fact]
    public void ClampsPageSizeAtMax_WhenAskedForMoreThanTheLimit()
    {
        var all = Range(1000);
        var ctx = new DefaultHttpContext();

        var result = PaginationHelper.Paginate(all, ctx, page: 1, pageSize: 10_000);

        Assert.Equal(PaginationHelper.MaxPageSize, result.Count);
    }

    [Fact]
    public void NormalisesBadPageInputs_ToFirstPageAndOneItem()
    {
        var all = Range(50);
        var ctx = new DefaultHttpContext();

        var result = PaginationHelper.Paginate(all, ctx, page: 0, pageSize: 0);

        Assert.Single(result);
        Assert.Equal(1, result[0]);
    }

    [Fact]
    public void ExposesTotalCountHeaderToBrowsers()
    {
        var all = Range(3);
        var ctx = new DefaultHttpContext();

        PaginationHelper.Paginate(all, ctx, page: null, pageSize: null);

        Assert.Contains("X-Total-Count",
            ctx.Response.Headers["Access-Control-Expose-Headers"].ToString());
    }

    [Fact]
    public void ReturnsEmpty_WhenPageIsBeyondTheData()
    {
        var all = Range(10);
        var ctx = new DefaultHttpContext();

        var result = PaginationHelper.Paginate(all, ctx, page: 5, pageSize: 10);

        Assert.Empty(result);
        Assert.Equal("10", ctx.Response.Headers["X-Total-Count"]);
    }
}
