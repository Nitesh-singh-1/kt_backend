using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using KTransport.API.Services;
using KTransport.API.Services.Email;
using KTransport.API.Services.Rbac;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KTransport.API.Tests.Services;

/// <summary>
/// Regression gate for the RBAC incident where tenant-admin role seeding and
/// the one-off <c>SeedNonAdminSystemRolePermissions</c> migration encoded the
/// same role→permission rules twice and drifted (6 non-admin roles seeded
/// with zero permissions across 3 tenants; Sneha Transport's admin missing
/// 69/135 permissions because seeding was module-scoped even for admin).
/// <see cref="SystemRolePermissionMatrix"/> is now the single source of
/// truth; these tests pin the two invariants that caused the incident.
/// </summary>
public class TenantServiceSystemRoleMatrixTests
{
    private sealed class StubTenantContext : ITenantContext
    {
        public Guid CurrentTenantId { get; private set; } = TenantConstants.DefaultTenantId;
        public bool HasTenant => true;
        public void SetTenantId(Guid tenantId) => CurrentTenantId = tenantId;
    }

    private sealed class StubEmail : IEmailSender
    {
        public bool IsEnabled => false;
        public Task<bool> SendAsync(string a, string b, string c, string? d = null) => Task.FromResult(true);
    }

    private sealed class StubAudit : IAuditLogService
    {
        public Task LogAsync(string action, bool success = true, Guid? tenantId = null, int? userId = null,
            string? username = null, string? entityType = null, string? entityId = null, string? details = null)
            => Task.CompletedTask;
    }

    private static KTransportDbContext NewContext()
    {
        var opts = new DbContextOptionsBuilder<KTransportDbContext>()
            .UseInMemoryDatabase($"RoleMatrix_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new KTransportDbContext(opts, new StubTenantContext());
    }

    private static TenantService NewTenantService(KTransportDbContext ctx)
        => new TenantService(
            NullLogger<TenantService>.Instance,
            ctx,
            Options.Create(new JwtSettings { Secret = "0123456789abcdef0123456789abcdef", Issuer = "t", Audience = "t", ExpiryInMinutes = 60 }),
            new StubAudit(),
            new StubEmail());

    private static async Task SeedCatalogAsync(KTransportDbContext ctx)
    {
        ctx.Modules.AddRange(
            new Module { Id = 1, Code = "dashboard", Name = "Dashboard" },
            new Module { Id = 2, Code = "bilty", Name = "Bilty" },
            new Module { Id = 3, Code = "billing", Name = "Billing" },
            new Module { Id = 4, Code = "reports", Name = "Reports" });
        ctx.Permissions.AddRange(
            new Permission { Key = "dashboard.view", FeatureKey = "dashboard", Action = "View" },
            new Permission { Key = "bilty.view", FeatureKey = "bilty", Action = "View" },
            new Permission { Key = "bilty.create", FeatureKey = "bilty", Action = "Create" },
            new Permission { Key = "billing.view", FeatureKey = "billing", Action = "View" },
            new Permission { Key = "billing.invoices.edit", FeatureKey = "billing.invoices", Action = "Edit" },
            new Permission { Key = "reports.view", FeatureKey = "reports", Action = "View" });
        ctx.Tenants.Add(new Tenant { Id = TenantConstants.DefaultTenantId, Name = "Platform", Code = "PLAT", IsActive = true, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Onboarding_AdminRole_GetsEveryCatalogPermission_EvenForDisabledModules()
    {
        // Root cause of "Sneha Transport's admin missing 69/135 permissions":
        // admin seeding was gated on enabled modules. Onboard with ONLY
        // dashboard + bilty enabled — admin must still get billing.view and
        // reports.view, whose modules are NOT enabled.
        await using var ctx = NewContext();
        await SeedCatalogAsync(ctx);
        var svc = NewTenantService(ctx);

        var res = await svc.OnboardTenantByAdminAsync(new TenantAdminOnboardRequest
        {
            OrganizationName = "Sneha Clone", OrganizationCode = "SNEHA2",
            AdminUsername = "sneha2", AdminPassword = "secret123", AdminFullName = "Sneha",
            AdminRoleCode = "admin",
            EnabledModuleCodes = new List<string> { "dashboard", "bilty" }
        }, platformAdminUserId: 1);
        Assert.True(res.Success);

        var adminGrants = ctx.RolePermissions
            .IgnoreQueryFilters()
            .Where(rp => rp.TenantId == res.TenantId && rp.RoleName == "admin" && rp.RevokedAt == null)
            .Select(rp => rp.PermissionKey)
            .ToList();

        var allPermissionKeys = ctx.Permissions.Select(p => p.Key).ToList();
        Assert.Equal(allPermissionKeys.Count, adminGrants.Distinct().Count());
        Assert.Contains("billing.view", adminGrants);
        Assert.Contains("reports.view", adminGrants);
    }

    [Fact]
    public async Task Onboarding_NonAdminRole_IsStillScopedToEnabledModules()
    {
        // Admin is the one exception. billing_executive must NOT get
        // billing.view when billing isn't an enabled module for the tenant.
        await using var ctx = NewContext();
        await SeedCatalogAsync(ctx);
        var svc = NewTenantService(ctx);

        var res = await svc.OnboardTenantByAdminAsync(new TenantAdminOnboardRequest
        {
            OrganizationName = "Scoped Co", OrganizationCode = "SCOPED",
            AdminUsername = "scoped1", AdminPassword = "secret123", AdminFullName = "Scoped",
            AdminRoleCode = "admin",
            EnabledModuleCodes = new List<string> { "dashboard", "bilty" }
        }, platformAdminUserId: 1);
        Assert.True(res.Success);

        var billingExecGrants = ctx.RolePermissions
            .IgnoreQueryFilters()
            .Where(rp => rp.TenantId == res.TenantId && rp.RoleName == "billing_executive" && rp.RevokedAt == null)
            .Select(rp => rp.PermissionKey)
            .ToList();

        Assert.DoesNotContain("billing.view", billingExecGrants);
    }

    [Fact]
    public async Task Onboarding_EveryNonAdminSystemRole_GetsAtLeastOnePermission()
    {
        // Root cause of "6 roles across all tenants had 0 permissions":
        // pin that every system role template seeds something when its
        // scope's module is enabled.
        await using var ctx = NewContext();
        await SeedCatalogAsync(ctx);
        var svc = NewTenantService(ctx);

        var res = await svc.OnboardTenantByAdminAsync(new TenantAdminOnboardRequest
        {
            OrganizationName = "Full Co", OrganizationCode = "FULLCO",
            AdminUsername = "fullco1", AdminPassword = "secret123", AdminFullName = "Full",
            AdminRoleCode = "admin",
            EnabledModuleCodes = new List<string> { "dashboard", "bilty", "billing", "reports" }
        }, platformAdminUserId: 1);
        Assert.True(res.Success);

        foreach (var code in SystemRolePermissionMatrix.SystemRoleCodes)
        {
            var count = ctx.RolePermissions
                .IgnoreQueryFilters()
                .Count(rp => rp.TenantId == res.TenantId && rp.RoleName == code && rp.RevokedAt == null);
            Assert.True(count > 0, $"role '{code}' was seeded with zero permissions");
        }
    }

    [Fact]
    public async Task NewRolePermissionRows_SetPermissionIdUnconditionally()
    {
        // TASK-049 invariant: new RolePermission inserts must carry
        // PermissionId, not just the legacy PermissionKey string.
        await using var ctx = NewContext();
        await SeedCatalogAsync(ctx);
        var svc = NewTenantService(ctx);

        var res = await svc.OnboardTenantByAdminAsync(new TenantAdminOnboardRequest
        {
            OrganizationName = "Pid Co", OrganizationCode = "PIDCO",
            AdminUsername = "pidco1", AdminPassword = "secret123", AdminFullName = "Pid",
            AdminRoleCode = "admin",
            EnabledModuleCodes = new List<string> { "dashboard", "bilty" }
        }, platformAdminUserId: 1);
        Assert.True(res.Success);

        var rows = ctx.RolePermissions.IgnoreQueryFilters().Where(rp => rp.TenantId == res.TenantId).ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, rp => Assert.True(rp.PermissionId.HasValue && rp.PermissionId.Value > 0));
    }

    [Fact]
    public async Task ReconcileSystemRolePermissionsForTenantAsync_IsAdditiveAndIdempotent()
    {
        await using var ctx = NewContext();
        await SeedCatalogAsync(ctx);
        var svc = NewTenantService(ctx);

        var res = await svc.OnboardTenantByAdminAsync(new TenantAdminOnboardRequest
        {
            OrganizationName = "Reconcile Co", OrganizationCode = "RECON",
            AdminUsername = "recon1", AdminPassword = "secret123", AdminFullName = "Recon",
            AdminRoleCode = "admin",
            EnabledModuleCodes = new List<string> { "dashboard", "bilty", "billing", "reports" }
        }, platformAdminUserId: 1);
        Assert.True(res.Success);
        var tenantId = res.TenantId!.Value;

        var before = ctx.RolePermissions.IgnoreQueryFilters().Count(rp => rp.TenantId == tenantId);

        // Simulate the exact incident: a permission got added to the catalog
        // after the tenant onboarded (viewer never saw it).
        var newPerm = new Permission { Key = "reports.export", FeatureKey = "reports", Action = "Export" };
        ctx.Permissions.Add(newPerm);
        await ctx.SaveChangesAsync();

        await svc.ReconcileSystemRolePermissionsForTenantAsync(tenantId, actingUserId: 1);
        var afterFirstReconcile = ctx.RolePermissions.IgnoreQueryFilters().Count(rp => rp.TenantId == tenantId);
        Assert.True(afterFirstReconcile > before, "reconcile should have picked up the new catalog permission");

        // Idempotent: calling it again must not create duplicates.
        await svc.ReconcileSystemRolePermissionsForTenantAsync(tenantId, actingUserId: 1);
        var afterSecondReconcile = ctx.RolePermissions.IgnoreQueryFilters().Count(rp => rp.TenantId == tenantId);
        Assert.Equal(afterFirstReconcile, afterSecondReconcile);
    }
}
