using System;
using System.Security.Claims;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KTransport.API.Tests.Services;

/// <summary>
/// Phase 1 of the RBAC hardening plan: the admin / super-user role definition
/// used to be hand-copied into five places that had drifted. These tests pin
/// the single source of truth (<see cref="RbacRoles"/>) and prove the live
/// authorization path (<see cref="FeatureAuthorizationService"/>) agrees with
/// it, so no non-admin role can slip through an admin gate.
/// </summary>
public class RbacRolesConsistencyTests
{
    private sealed class StubTenantContext : ITenantContext
    {
        public Guid CurrentTenantId { get; private set; } = TenantConstants.DefaultTenantId;
        public bool HasTenant => true;
        public void SetTenantId(Guid tenantId) => CurrentTenantId = tenantId;
    }

    private static FeatureAuthorizationService NewService()
    {
        var opts = new DbContextOptionsBuilder<KTransportDbContext>()
            .UseInMemoryDatabase($"RbacRoles_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new FeatureAuthorizationService(new KTransportDbContext(opts, new StubTenantContext()));
    }

    private static ClaimsPrincipal UserWith(string role, Guid tenantId)
        => new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, role),
            new Claim("tenant_id", tenantId.ToString()),
        }, authenticationType: "test"));

    [Theory]
    [InlineData("admin")]
    [InlineData("superadmin")]
    [InlineData("tenantadmin")]
    [InlineData("super_user")]
    [InlineData("SUPER_USER")]   // case-insensitive
    [InlineData("tenant_owner")]
    [InlineData("TENANT_OWNER")] // case-insensitive
    public void IsAdminRole_TrueForEveryKnownAdminAlias(string role)
    {
        Assert.True(RbacRoles.IsAdminRole(role));
    }

    [Theory]
    [InlineData("operations_manager")]
    [InlineData("supervisor")]
    [InlineData("operator")]
    [InlineData("billing_executive")]
    [InlineData("accountant")]
    [InlineData("viewer")]
    [InlineData("sub_user")]
    [InlineData("manager")]      // a plausible custom role name
    [InlineData("administrator")] // NOT an alias — must not pass
    [InlineData("")]
    [InlineData(null)]
    public void IsAdminRole_FalseForNonAdminRoles(string? role)
    {
        Assert.False(RbacRoles.IsAdminRole(role));
    }

    [Fact]
    public void FeatureAuthorizationService_IsSuperUser_AgreesWithRbacRoles()
    {
        var svc = NewService();
        var tid = Guid.NewGuid();

        // Every admin alias the single source of truth recognizes must also
        // pass the live authorization service — no drift between the two.
        foreach (var code in RbacRoles.AdminRoleCodes)
        {
            Assert.True(svc.IsSuperUser(UserWith(code, tid)), $"IsSuperUser disagreed for admin alias '{code}'");
        }

        Assert.False(svc.IsSuperUser(UserWith("viewer", tid)));
        Assert.False(svc.IsSuperUser(UserWith("operator", tid)));
    }

    [Fact]
    public void IsPlatformAdmin_RequiresDefaultTenantMembership()
    {
        var svc = NewService();

        // admin in the platform/default tenant → platform admin.
        Assert.True(svc.IsPlatformAdmin(UserWith("admin", TenantConstants.DefaultTenantId)));

        // Same admin role, but a non-default tenant → tenant admin only, NOT platform.
        Assert.False(svc.IsPlatformAdmin(UserWith("admin", Guid.NewGuid())));

        // Non-admin in the default tenant → not platform admin.
        Assert.False(svc.IsPlatformAdmin(UserWith("viewer", TenantConstants.DefaultTenantId)));
    }

    [Fact]
    public void ReservedRoleCodes_BlockCustomRolesFromMintingAdminAccess()
    {
        // Guard for the upcoming role-CRUD work: a tenant must not be able to
        // create a custom role whose code is an admin alias.
        Assert.True(RbacRoles.IsReservedRoleCode("admin"));
        Assert.True(RbacRoles.IsReservedRoleCode("SUPER_USER"));
        Assert.True(RbacRoles.IsReservedRoleCode("superadmin"));
        Assert.False(RbacRoles.IsReservedRoleCode("dispatch_lead"));
        Assert.False(RbacRoles.IsReservedRoleCode("night_shift"));
    }
}
