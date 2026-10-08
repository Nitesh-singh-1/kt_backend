using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KTransport.API.Tests.Services;

/// <summary>
/// TASK-044 Phase 3 — entitlements normalized tables are the sole source of
/// truth. The legacy menu_entitlements_json column was dropped. These tests
/// cover the contract's `tests_required` list plus permission-set regression
/// tests on the normalized schema.
/// </summary>
public class EntitlementsServiceTests
{
    private static readonly Guid TestTenantId = TenantConstants.DefaultTenantId;

    private sealed class StubTenantContext : ITenantContext
    {
        public Guid CurrentTenantId { get; private set; } = TestTenantId;
        public bool HasTenant => true;
        public void SetTenantId(Guid tenantId) => CurrentTenantId = tenantId;
    }

    private static KTransportDbContext NewContext()
    {
        var opts = new DbContextOptionsBuilder<KTransportDbContext>()
            .UseInMemoryDatabase(databaseName: $"EntitlementsTests_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new KTransportDbContext(opts, new StubTenantContext());
    }

    private static EntitlementsService NewService(KTransportDbContext ctx)
        => new EntitlementsService(ctx, NullLogger<EntitlementsService>.Instance);

    private static void SeedSubscription(KTransportDbContext ctx, params string[] enabledFeatureKeys)
    {
        ctx.TenantSettings.Add(new TenantSetting { TenantId = TestTenantId, CreatedAt = DateTime.UtcNow });
        ctx.TenantEntitlementSubscriptions.Add(new TenantEntitlementSubscription
        {
            TenantId = TestTenantId,
            PlanTier = "Enterprise",
            EnabledFeatureKeys = enabledFeatureKeys.ToList(),
            EffectiveFrom = DateTime.UtcNow,
            EffectiveUntil = null
        });
        ctx.SaveChanges();
    }

    private static User SeedUser(KTransportDbContext ctx, int id, string username)
    {
        var u = new User
        {
            Id = id,
            TenantId = TestTenantId,
            Username = username,
            Password = "x",
            FullName = username,
            Role = "SUB_USER",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Users.Add(u);
        ctx.SaveChanges();
        return u;
    }

    // ---- Phase 3 contract test 1: BackfillFromJsonAsync is no-op + obsolete ----
    [Fact]
#pragma warning disable CS0618
    public async Task Phase3_BackfillFromJsonAsync_IsNoOp_ReturnsEmptySummary()
    {
        await using var ctx = NewContext();
        SeedSubscription(ctx, "billing", "billing.invoices");

        var svc = NewService(ctx);
        var summary = await svc.BackfillFromJsonAsync();

        Assert.Equal(0, summary.TenantsProcessed);
        Assert.Equal(0, summary.RolePermissionsInserted);
        Assert.Equal(0, summary.UserOverridesInserted);
        Assert.Equal(0, summary.SubscriptionsInserted);
    }
#pragma warning restore CS0618

    // ---- Phase 3 contract test 2: WriteTenantEntitlementsAsync creates rows ----
    [Fact]
    public async Task Phase3_WriteTenantEntitlementsAsync_RoleGrant_WritesToRolePermissions()
    {
        await using var ctx = NewContext();
        ctx.TenantSettings.Add(new TenantSetting { TenantId = TestTenantId, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var dto = new TenantMenuEntitlementsDto
        {
            TenantId = TestTenantId,
            PlanTier = "Enterprise",
            EnabledMenuKeys = new List<string> { "dashboard", "billing", "billing.invoices" },
            RoleOverrides = new Dictionary<string, List<string>>
            {
                { "admin", new List<string> { "billing.invoices" } }
            }
        };

        var svc = NewService(ctx);
        await svc.WriteTenantEntitlementsAsync(TestTenantId, dto, userId: 1);

        var rolePerms = await ctx.RolePermissions.IgnoreQueryFilters().ToListAsync();
        Assert.NotEmpty(rolePerms);
        Assert.All(rolePerms, r => Assert.StartsWith("billing.invoices.", r.PermissionKey));
        var sub = await ctx.TenantEntitlementSubscriptions.IgnoreQueryFilters().FirstAsync();
        Assert.Contains("billing.invoices", sub.EnabledFeatureKeys);
    }

    // ---- Phase 3 contract test 3: WriteTenantEntitlementsAsync IGNORES legacy JSON fields ----
    [Fact]
    public async Task Phase3_WriteTenantEntitlements_IgnoresLegacyJsonFields()
    {
        await using var ctx = NewContext();
        ctx.TenantSettings.Add(new TenantSetting { TenantId = TestTenantId, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

#pragma warning disable CS0618
        var dto = new TenantMenuEntitlementsDto
        {
            TenantId = TestTenantId,
            PlanTier = "Enterprise",
            EnabledMenuKeys = new List<string> { "dashboard", "billing", "billing.invoices" },
            RoleOverridesJson = "{\"admin\":[\"billing.invoices\"]}", // should be IGNORED
            UserOverridesJson = "{\"1\":[\"billing.invoices\"]}"      // should be IGNORED
        };
#pragma warning restore CS0618

        var svc = NewService(ctx);
        await svc.WriteTenantEntitlementsAsync(TestTenantId, dto, userId: 1);

        var rolePerms = await ctx.RolePermissions.IgnoreQueryFilters().ToListAsync();
        var userOverrides = await ctx.UserPermissionOverrides.IgnoreQueryFilters().ToListAsync();

        Assert.Empty(rolePerms);
        Assert.Empty(userOverrides);
    }

    // ---- Phase 3 contract test 4: ComputeEffectivePermissions reads tables ----
    [Fact]
    public async Task Phase3_ComputeEffectivePermissions_InAction_SplitSet()
    {
        await using var ctx = NewContext();
        SeedUser(ctx, 9, "nitesh");
        SeedSubscription(ctx, "billing", "billing.invoices");
        ctx.RolePermissions.Add(new RolePermission
        {
            TenantId = TestTenantId,
            RoleName = "admin",
            PermissionKey = "billing.invoices.view",
            GrantedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var svc = NewService(ctx);
        var effective = await svc.ComputeEffectivePermissionsFromTablesAsync(TestTenantId, null, "admin");

        Assert.Contains("billing.invoices.view", effective, StringComparer.OrdinalIgnoreCase);
    }

    // ---- Phase 3 contract test 5: tenant subscription gate ----
    [Fact]
    public async Task Phase3_TenantNotSubscribed_FeatureStripped()
    {
        await using var ctx = NewContext();
        SeedSubscription(ctx, "dashboard");
        ctx.RolePermissions.Add(new RolePermission
        {
            TenantId = TestTenantId,
            RoleName = "sub_user",
            PermissionKey = "billing.bill_book.create",
            GrantedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var svc = NewService(ctx);
        var effective = await svc.ComputeEffectivePermissionsFromTablesAsync(TestTenantId, null, "sub_user");

        Assert.DoesNotContain("billing.bill_book.create", effective, StringComparer.OrdinalIgnoreCase);
    }

    // ---- Phase 3 contract test 6: user override wins over role ----
    [Fact]
    public async Task Phase3_UserOverride_UnionsWithRole()
    {
        // ADR authorization-rbac-architecture.md §F + .agent/RULES/AUTHORIZATION.md:
        //   effective = (role grants ∪ user grants ∖ user revokes) ∩ tenant_modules
        // Pre-TASK-046 bug (reproduced by the Nitesh menu-missing report 2026-10-08):
        // the resolver treated user overrides as a REPLACEMENT for role grants, so a
        // single user_permission_overrides row masked every role_permissions entry.
        await using var ctx = NewContext();
        SeedUser(ctx, 9, "nitesh");
        SeedSubscription(ctx, "billing", "billing.bill_book", "billing.invoices");
        ctx.RolePermissions.Add(new RolePermission
        {
            TenantId = TestTenantId,
            RoleName = "sub_user",
            PermissionKey = "billing.bill_book.create",
            GrantedAt = DateTime.UtcNow
        });
        ctx.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = TestTenantId,
            UserId = 9,
            PermissionKey = "billing.invoices.view",
            IsGranted = true,
            GrantedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var svc = NewService(ctx);
        var effective = await svc.ComputeEffectivePermissionsFromTablesAsync(TestTenantId, 9, "sub_user");

        Assert.Contains("billing.invoices.view", effective, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("billing.bill_book.create", effective, StringComparer.OrdinalIgnoreCase);
    }

    // ---- ADR §F revoke semantics: is_granted=false subtracts from the union ----
    [Fact]
    public async Task Phase3_UserRevoke_SubtractsFromRoleGrants()
    {
        await using var ctx = NewContext();
        SeedUser(ctx, 10, "nitesh2");
        SeedSubscription(ctx, "billing", "billing.bill_book", "billing.invoices");
        ctx.RolePermissions.Add(new RolePermission
        {
            TenantId = TestTenantId,
            RoleName = "sub_user",
            PermissionKey = "billing.bill_book.create",
            GrantedAt = DateTime.UtcNow
        });
        ctx.RolePermissions.Add(new RolePermission
        {
            TenantId = TestTenantId,
            RoleName = "sub_user",
            PermissionKey = "billing.invoices.view",
            GrantedAt = DateTime.UtcNow
        });
        ctx.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = TestTenantId,
            UserId = 10,
            PermissionKey = "billing.bill_book.create",
            IsGranted = false,
            GrantedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var svc = NewService(ctx);
        var effective = await svc.ComputeEffectivePermissionsFromTablesAsync(TestTenantId, 10, "sub_user");

        Assert.DoesNotContain("billing.bill_book.create", effective, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("billing.invoices.view", effective, StringComparer.OrdinalIgnoreCase);
    }

    // ---- Phase 3 contract test 7: WriteTenantEntitlements records GrantedBy ----
    [Fact]
    public async Task Phase3_WriteTenantEntitlements_RecordsActingUserOnGrantedBy()
    {
        await using var ctx = NewContext();
        SeedUser(ctx, 7, "tenantadmin");
        ctx.TenantSettings.Add(new TenantSetting { TenantId = TestTenantId, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var dto = new TenantMenuEntitlementsDto
        {
            TenantId = TestTenantId,
            PlanTier = "Enterprise",
            EnabledMenuKeys = new List<string> { "dashboard", "billing", "billing.invoices" },
            RoleOverrides = new Dictionary<string, List<string>> { { "admin", new List<string> { "billing.invoices" } } },
            UserOverrides = new Dictionary<string, List<string>> { { "7", new List<string> { "billing.invoices" } } }
        };

        var svc = NewService(ctx);
        await svc.WriteTenantEntitlementsAsync(TestTenantId, dto, userId: 7);

        var rolePerms = await ctx.RolePermissions.IgnoreQueryFilters().ToListAsync();
        var userOverrides = await ctx.UserPermissionOverrides.IgnoreQueryFilters().ToListAsync();

        Assert.NotEmpty(rolePerms);
        Assert.NotEmpty(userOverrides);
        Assert.All(rolePerms, r => Assert.Equal(7, r.GrantedBy));
        Assert.All(userOverrides, o => Assert.Equal(7, o.GrantedBy));
    }

    // ---- Phase 3 contract test 8: idempotent re-run inserts zero duplicates ----
    [Fact]
    public async Task Phase3_WriteTenantEntitlements_IsIdempotent()
    {
        await using var ctx = NewContext();
        SeedUser(ctx, 7, "tenantadmin");
        ctx.TenantSettings.Add(new TenantSetting { TenantId = TestTenantId, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var dto = new TenantMenuEntitlementsDto
        {
            TenantId = TestTenantId,
            PlanTier = "Enterprise",
            EnabledMenuKeys = new List<string> { "dashboard", "billing", "billing.invoices" },
            RoleOverrides = new Dictionary<string, List<string>> { { "admin", new List<string> { "billing.invoices" } } }
        };

        var svc = NewService(ctx);
        await svc.WriteTenantEntitlementsAsync(TestTenantId, dto, userId: 7);
        var afterFirst = await ctx.RolePermissions.IgnoreQueryFilters().CountAsync();
        await svc.WriteTenantEntitlementsAsync(TestTenantId, dto, userId: 7);
        var afterSecond = await ctx.RolePermissions.IgnoreQueryFilters().CountAsync();

        Assert.Equal(afterFirst, afterSecond);
    }

    // ---- Regression: effective set keeps action suffixes ----
    [Fact]
    public async Task EffectiveSet_KeepsActionSuffixes()
    {
        await using var ctx = NewContext();
        SeedUser(ctx, 9, "nitesh");
        SeedSubscription(ctx, "billing", "billing.bill_book");
        foreach (var action in new[] { "view", "create", "edit", "delete", "print" })
        {
            ctx.RolePermissions.Add(new RolePermission
            {
                TenantId = TestTenantId,
                RoleName = "sub_user",
                PermissionKey = $"billing.bill_book.{action}",
                GrantedAt = DateTime.UtcNow
            });
        }
        await ctx.SaveChangesAsync();

        var svc = NewService(ctx);
        var effective = await svc.ComputeEffectivePermissionsFromTablesAsync(TestTenantId, null, "sub_user");

        Assert.Equal(5, effective.Count);
        Assert.Contains("billing.bill_book.view", effective, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("billing.bill_book.print", effective, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("billing.bill_book", effective, StringComparer.OrdinalIgnoreCase);
    }

    // ---- Regression: NavigationService.GetUserPermissionsAsync returns action-split keys ----
    [Fact]
    public async Task GetUserPermissions_ReturnsActionSplitKeys()
    {
        await using var ctx = NewContext();
        SeedUser(ctx, 9, "nitesh");
        SeedSubscription(ctx, "dashboard", "billing", "billing.bill_book");
        foreach (var action in new[] { "view", "create", "edit", "delete", "print" })
        {
            ctx.RolePermissions.Add(new RolePermission
            {
                TenantId = TestTenantId,
                RoleName = "sub_user",
                PermissionKey = $"billing.bill_book.{action}",
                GrantedAt = DateTime.UtcNow
            });
        }
        await ctx.SaveChangesAsync();

        var ent = new EntitlementsService(ctx, NullLogger<EntitlementsService>.Instance);
        var nav = new NavigationService(ctx, ent, NullLogger<NavigationService>.Instance);
        var perms = await nav.GetUserPermissionsAsync(TenantConstants.DefaultTenantId, "sub_user", "9");

        Assert.Contains("billing.bill_book.view", perms, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("billing.bill_book.create", perms, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("billing.bill_book.edit", perms, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("billing.bill_book.delete", perms, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("billing.bill_book.print", perms, StringComparer.OrdinalIgnoreCase);
    }
}
