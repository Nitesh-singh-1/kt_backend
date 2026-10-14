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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KTransport.API.Tests.Services;

/// <summary>
/// Phase 4 of the RBAC hardening plan: custom tenant role management. Proves a
/// tenant admin can mint a role with a chosen permission set, that the role
/// integrates with the live effective-permission pipeline, and that the guard
/// rails hold (reserved codes rejected, system roles read-only, deactivation
/// actually drops access).
/// </summary>
public class RoleManagementServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class StubTenantContext : ITenantContext
    {
        public Guid CurrentTenantId { get; private set; } = TenantId;
        public bool HasTenant => true;
        public void SetTenantId(Guid tenantId) => CurrentTenantId = tenantId;
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
            .UseInMemoryDatabase($"RoleMgmt_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new KTransportDbContext(opts, new StubTenantContext());
    }

    private static RoleManagementService NewService(KTransportDbContext ctx)
        => new(ctx, new StubAudit(), NullLogger<RoleManagementService>.Instance);

    private static EntitlementsService NewEntitlements(KTransportDbContext ctx)
        => new(ctx, NullLogger<EntitlementsService>.Instance);

    private static async Task SeedAsync(KTransportDbContext ctx)
    {
        ctx.Tenants.Add(new Tenant { Id = TenantId, Name = "Role Test", Code = "ROLE", IsActive = true, CreatedAt = DateTime.UtcNow });
        ctx.Modules.AddRange(
            new Module { Id = 1, Code = "dashboard", Name = "Dashboard" },
            new Module { Id = 2, Code = "bilty", Name = "Bilty" },
            new Module { Id = 3, Code = "pod", Name = "POD" },
            new Module { Id = 4, Code = "billing", Name = "Billing" }); // NOT enabled for this tenant
        ctx.Permissions.AddRange(
            new Permission { Id = 1, Key = "dashboard.view", FeatureKey = "dashboard", Action = "View", ModuleId = 1 },
            new Permission { Id = 2, Key = "consignments.create.create", FeatureKey = "consignments.create", Action = "Create", ModuleId = 2 },
            new Permission { Id = 3, Key = "consignments.create.view", FeatureKey = "consignments.create", Action = "View", ModuleId = 2 },
            new Permission { Id = 4, Key = "pod.view", FeatureKey = "pod", Action = "View", ModuleId = 3 },
            // billing permission exists in the catalog but the tenant did NOT pay for the billing module:
            new Permission { Id = 5, Key = "billing.invoices.delete", FeatureKey = "billing.invoices", Action = "Delete", ModuleId = 4 });
        // Tenant has dashboard + bilty + pod enabled (NOT billing) so only those are assignable/effective.
        ctx.TenantModules.AddRange(
            new TenantModule { TenantId = TenantId, ModuleId = 1, IsEnabled = true },
            new TenantModule { TenantId = TenantId, ModuleId = 2, IsEnabled = true },
            new TenantModule { TenantId = TenantId, ModuleId = 3, IsEnabled = true });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateRole_persists_custom_role_and_grants()
    {
        await using var ctx = NewContext();
        await SeedAsync(ctx);
        var svc = NewService(ctx);

        var (ok, _, data) = await svc.CreateRoleAsync(TenantId, new CreateRoleRequest
        {
            Code = "Dispatch Lead",   // spaces → normalized to dispatch_lead
            Name = "Dispatch Lead",
            PermissionKeys = new List<string> { "consignments.create.create", "consignments.create.view", "pod.view" }
        }, actingUserId: 1);

        Assert.True(ok);
        Assert.NotNull(data);
        Assert.Equal("dispatch_lead", data!.Code);
        Assert.False(data.IsSystem);
        Assert.Equal(3, data.PermissionKeys.Count);

        var row = ctx.Roles.IgnoreQueryFilters().Single(r => r.TenantId == TenantId && r.Code == "dispatch_lead");
        Assert.False(row.IsSystem);
        Assert.Equal(3, ctx.RolePermissions.IgnoreQueryFilters().Count(rp => rp.RoleId == row.Id && rp.RevokedAt == null));
    }

    [Fact]
    public async Task CreateRole_rejects_reserved_admin_code()
    {
        await using var ctx = NewContext();
        await SeedAsync(ctx);
        var svc = NewService(ctx);

        foreach (var code in new[] { "admin", "superadmin", "SUPER_USER", "tenant_owner" })
        {
            var (ok, message, _) = await svc.CreateRoleAsync(TenantId, new CreateRoleRequest
            {
                Code = code, Name = "x", PermissionKeys = new List<string>()
            }, actingUserId: 1);
            Assert.False(ok);
            Assert.Contains("reserved", message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task CreateRole_rejects_duplicate_code_and_unknown_permission()
    {
        await using var ctx = NewContext();
        await SeedAsync(ctx);
        var svc = NewService(ctx);

        var first = await svc.CreateRoleAsync(TenantId, new CreateRoleRequest { Code = "ops_x", Name = "Ops X" }, 1);
        Assert.True(first.Success);

        var dup = await svc.CreateRoleAsync(TenantId, new CreateRoleRequest { Code = "ops_x", Name = "Ops X Again" }, 1);
        Assert.False(dup.Success);
        Assert.Contains("already exists", dup.Message, StringComparison.OrdinalIgnoreCase);

        var bad = await svc.CreateRoleAsync(TenantId, new CreateRoleRequest
        {
            Code = "ops_y", Name = "Ops Y", PermissionKeys = new List<string> { "billing.not_a_real_key" }
        }, 1);
        Assert.False(bad.Success);
        Assert.Contains("Unknown permission", bad.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateRole_reconciles_permissions_and_blocks_system_roles()
    {
        await using var ctx = NewContext();
        await SeedAsync(ctx);
        // seed a system role
        ctx.Roles.Add(new Role { TenantId = TenantId, Code = "admin", Name = "Administrator", IsSystem = true, IsActive = true });
        await ctx.SaveChangesAsync();
        var svc = NewService(ctx);

        var created = await svc.CreateRoleAsync(TenantId, new CreateRoleRequest
        {
            Code = "night_shift", Name = "Night Shift",
            PermissionKeys = new List<string> { "consignments.create.create", "consignments.create.view" }
        }, 1);
        Assert.True(created.Success);
        var roleId = created.Data!.Id;

        // Reconcile: drop create.create, add pod.view, keep create.view.
        var upd = await svc.UpdateRoleAsync(TenantId, roleId, new UpdateRoleRequest
        {
            PermissionKeys = new List<string> { "consignments.create.view", "pod.view" }
        }, 1);
        Assert.True(upd.Success);
        var active = ctx.RolePermissions.IgnoreQueryFilters()
            .Where(rp => rp.RoleId == roleId && rp.RevokedAt == null).Select(rp => rp.PermissionKey).ToHashSet();
        Assert.Contains("consignments.create.view", active);
        Assert.Contains("pod.view", active);
        Assert.DoesNotContain("consignments.create.create", active);
        // removed grant is soft-revoked, not deleted
        Assert.Contains(ctx.RolePermissions.IgnoreQueryFilters(),
            rp => rp.RoleId == roleId && rp.PermissionKey == "consignments.create.create" && rp.RevokedAt != null);

        // System role edits are rejected.
        var sysRole = ctx.Roles.IgnoreQueryFilters().Single(r => r.Code == "admin");
        var sysUpd = await svc.UpdateRoleAsync(TenantId, sysRole.Id, new UpdateRoleRequest { Name = "hacked" }, 1);
        Assert.False(sysUpd.Success);
        Assert.Contains("System roles", sysUpd.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRole_rejects_permission_outside_tenant_paid_modules()
    {
        await using var ctx = NewContext();
        await SeedAsync(ctx);
        var svc = NewService(ctx);

        // billing.invoices.delete is a real catalog key, but the tenant did NOT
        // pay for the billing module — the admin must not be able to assign it.
        var (ok, message, _) = await svc.CreateRoleAsync(TenantId, new CreateRoleRequest
        {
            Code = "over_reach", Name = "Over Reach",
            PermissionKeys = new List<string> { "consignments.create.view", "billing.invoices.delete" }
        }, actingUserId: 1);

        Assert.False(ok);
        Assert.Contains("plan", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("billing.invoices.delete", message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(ctx.Roles.IgnoreQueryFilters().FirstOrDefault(r => r.Code == "over_reach"));
    }

    [Fact]
    public async Task AssignablePermissions_returns_only_paid_modules()
    {
        await using var ctx = NewContext();
        await SeedAsync(ctx);
        var svc = NewService(ctx);

        var dto = await svc.GetAssignablePermissionsAsync(TenantId);
        var moduleCodes = dto.Modules.Select(m => m.ModuleCode).ToList();

        Assert.Contains("bilty", moduleCodes);
        Assert.Contains("pod", moduleCodes);
        Assert.Contains("dashboard", moduleCodes);
        Assert.DoesNotContain("billing", moduleCodes); // not paid for → not offered
        // the billing delete key must not appear anywhere in the matrix
        var allKeys = dto.Modules.SelectMany(m => m.Pages).SelectMany(p => p.Actions).Select(a => a.Key).ToList();
        Assert.DoesNotContain("billing.invoices.delete", allKeys);
    }

    [Fact]
    public async Task CustomRole_grants_effective_permissions_end_to_end()
    {
        await using var ctx = NewContext();
        await SeedAsync(ctx);
        var svc = NewService(ctx);

        var created = await svc.CreateRoleAsync(TenantId, new CreateRoleRequest
        {
            Code = "dock_clerk", Name = "Dock Clerk",
            PermissionKeys = new List<string> { "pod.view", "consignments.create.view" }
        }, 1);
        Assert.True(created.Success);
        var roleId = created.Data!.Id;

        var user = new User { TenantId = TenantId, Username = "clerk1", Password = "x", FullName = "Clerk", Role = "dock_clerk", IsActive = true, CreatedAt = DateTime.UtcNow };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        ctx.UserRoles.Add(new UserRole { TenantId = TenantId, UserId = user.Id, RoleId = roleId, AssignedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var eff = await NewEntitlements(ctx).ComputeEffectivePermissionsFromTablesAsync(TenantId, user.Id, "dock_clerk");
        Assert.Contains("pod.view", eff);
        Assert.Contains("consignments.create.view", eff);
        Assert.DoesNotContain("consignments.create.create", eff);

        // Deactivate the role → the user's effective set drops its grants.
        var deact = await svc.DeactivateRoleAsync(TenantId, roleId, 1);
        Assert.True(deact.Success);
        var effAfter = await NewEntitlements(ctx).ComputeEffectivePermissionsFromTablesAsync(TenantId, user.Id, "dock_clerk");
        Assert.DoesNotContain("pod.view", effAfter);
        Assert.DoesNotContain("consignments.create.view", effAfter);
        // role row still exists (soft) — not deleted
        Assert.NotNull(ctx.Roles.IgnoreQueryFilters().FirstOrDefault(r => r.Id == roleId && !r.IsActive));
    }
}
