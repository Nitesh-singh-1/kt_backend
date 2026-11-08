using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KTransport.API.Tests.Security;

/// <summary>
/// Standing cross-tenant isolation guard required by AUTHORIZATION.md
/// ("Testing baseline" §3: a request from tenant A for tenant B's resource id
/// must NOT return the row). Two layers are proven here:
///
///  1. The EF global query filter (<c>HasQueryFilter</c> on every
///     <see cref="ITenantScopedEntity"/>) hides other tenants' rows from a
///     normal query, so a by-id lookup for another tenant's row returns null —
///     the endpoint then 404s rather than leaking the row. This is the
///     practical equivalent of the rulebook's <c>[EnforceTenantScope]</c>.
///
///  2. <see cref="TenantContext"/> resolves the tenant from the JWT claim only
///     for an authenticated caller, and ignores a spoofed <c>X-Tenant-ID</c>
///     header — so a request-supplied tenant id is not a cross-tenant vector
///     (AUTHORIZATION.md Rule 3).
/// </summary>
public class TenantIsolationTests
{
    private static readonly Guid TenantA = TenantConstants.DefaultTenantId;
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Mutable stub so a single context can "become" a different tenant mid-test.</summary>
    private sealed class MutableTenantContext : ITenantContext
    {
        public Guid CurrentTenantId { get; private set; } = TenantA;
        public bool HasTenant => CurrentTenantId != Guid.Empty;
        public void SetTenantId(Guid tenantId) => CurrentTenantId = tenantId;
    }

    private static KTransportDbContext NewContext(ITenantContext tenant)
    {
        var opts = new DbContextOptionsBuilder<KTransportDbContext>()
            .UseInMemoryDatabase($"Isolation_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new KTransportDbContext(opts, tenant);
    }

    [Fact]
    public async Task QueryFilter_hides_other_tenants_rows_from_a_normal_query()
    {
        var tenant = new MutableTenantContext();
        await using var db = NewContext(tenant);

        // Seed one role per tenant (IgnoreQueryFilters so both inserts land
        // regardless of the ambient tenant).
        db.Roles.Add(new Role { TenantId = TenantA, Code = "role_a", Name = "Role A", IsSystem = false, IsActive = true });
        db.Roles.Add(new Role { TenantId = TenantB, Code = "role_b", Name = "Role B", IsSystem = false, IsActive = true });
        await db.SaveChangesAsync();

        tenant.SetTenantId(TenantB);
        var visible = await db.Roles.ToListAsync();

        Assert.All(visible, r => Assert.Equal(TenantB, r.TenantId));
        Assert.DoesNotContain(visible, r => r.Code == "role_a");
        Assert.Contains(visible, r => r.Code == "role_b");
    }

    [Fact]
    public async Task ById_lookup_for_another_tenants_row_returns_null()
    {
        var tenant = new MutableTenantContext();
        await using var db = NewContext(tenant);

        var rowA = new Role { TenantId = TenantA, Code = "role_a", Name = "Role A", IsSystem = false, IsActive = true };
        db.Roles.Add(rowA);
        await db.SaveChangesAsync();
        var idA = rowA.Id;

        // Attacker is in tenant B and knows tenant A's row id.
        tenant.SetTenantId(TenantB);
        var leaked = await db.Roles.FirstOrDefaultAsync(r => r.Id == idA);

        Assert.Null(leaked); // filtered out → controller 404s, no cross-tenant read
    }

    [Fact]
    public void TenantContext_authenticated_caller_ignores_spoofed_header()
    {
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("tenant_id", TenantA.ToString()) },
            authenticationType: "jwt")); // authenticationType set ⇒ IsAuthenticated == true
        http.Request.Headers["X-Tenant-ID"] = TenantB.ToString();

        var ctx = new TenantContext(new StaticHttpContextAccessor(http));

        Assert.Equal(TenantA, ctx.CurrentTenantId); // JWT wins; header ignored
    }

    [Fact]
    public void TenantContext_unauthenticated_caller_may_use_header()
    {
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity()); // no auth type ⇒ not authenticated
        http.Request.Headers["X-Tenant-ID"] = TenantB.ToString();

        var ctx = new TenantContext(new StaticHttpContextAccessor(http));

        Assert.Equal(TenantB, ctx.CurrentTenantId); // anonymous flow may scope via header
    }

    private sealed class StaticHttpContextAccessor : IHttpContextAccessor
    {
        public StaticHttpContextAccessor(HttpContext ctx) => HttpContext = ctx;
        public HttpContext? HttpContext { get; set; }
    }
}
