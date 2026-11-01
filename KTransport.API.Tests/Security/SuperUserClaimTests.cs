using System;
using System.Collections.Generic;
using System.Security.Claims;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KTransport.API.Tests.Security;

/// <summary>
/// R+0 of the RBAC legacy-column cutover ADR: FeatureAuthorizationService.IsSuperUser
/// must authorize from the authoritative `is_super_user` claim (resolved from DB
/// grants at token issue), and fall back to the legacy role string ONLY when the
/// claim is absent (pre-R+0 tokens). This proves the JWT role string is no longer
/// security-load-bearing once the claim is present.
/// </summary>
public class SuperUserClaimTests
{
    private static readonly Guid Default = TenantContext.DefaultTenantId;
    private static readonly Guid Other = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static FeatureAuthorizationService NewService()
    {
        var opts = new DbContextOptionsBuilder<KTransportDbContext>()
            .UseInMemoryDatabase($"SuClaim_{Guid.NewGuid():N}")
            .Options;
        // IsSuperUser / IsPlatformAdmin read claims only; the context is unused here.
        return new FeatureAuthorizationService(new KTransportDbContext(opts, null!));
    }

    private static ClaimsPrincipal Principal(string? role, string? superFlag, Guid? tenant)
    {
        var claims = new List<Claim>();
        if (role != null) claims.Add(new Claim(ClaimTypes.Role, role));
        if (superFlag != null) claims.Add(new Claim("is_super_user", superFlag));
        if (tenant != null) claims.Add(new Claim("tenant_id", tenant.ToString()!));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "jwt"));
    }

    [Fact]
    public void Claim_true_wins_even_when_role_string_is_not_admin()
    {
        var svc = NewService();
        Assert.True(svc.IsSuperUser(Principal(role: "user", superFlag: "true", tenant: Default)));
    }

    [Fact]
    public void Claim_false_wins_even_when_role_string_is_admin()
    {
        var svc = NewService();
        // The whole point of R+0: a stale/mutable role string must not grant access
        // when the authoritative DB-resolved claim says otherwise.
        Assert.False(svc.IsSuperUser(Principal(role: "admin", superFlag: "false", tenant: Default)));
    }

    [Fact]
    public void No_claim_falls_back_to_legacy_role_string()
    {
        var svc = NewService();
        Assert.True(svc.IsSuperUser(Principal(role: "admin", superFlag: null, tenant: Default)));
        Assert.False(svc.IsSuperUser(Principal(role: "user", superFlag: null, tenant: Default)));
    }

    [Fact]
    public void PlatformAdmin_requires_super_user_claim_and_default_tenant()
    {
        var svc = NewService();
        Assert.True(svc.IsPlatformAdmin(Principal(role: "user", superFlag: "true", tenant: Default)));
        Assert.False(svc.IsPlatformAdmin(Principal(role: "user", superFlag: "true", tenant: Other)));
        Assert.False(svc.IsPlatformAdmin(Principal(role: "admin", superFlag: "false", tenant: Default)));
    }
}
