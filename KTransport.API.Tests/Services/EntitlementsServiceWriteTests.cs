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
/// TASK-049 Option B — write-side regressions for the three deleted auto-grant
/// bugs (menu.md §2 bugs 1/2/3). The central assertion is
/// <see cref="Assign_consignments_only_does_not_grant_delivery_settlement"/>:
/// enabling the <c>bilty</c> module must NOT grant any
/// <c>delivery_settlement.*</c> permission. Prior patches (TASK-041..048) all
/// re-introduced that grant through "helper" branches; the Option B rewrite
/// removes them structurally.
/// </summary>
public class EntitlementsServiceWriteTests
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
            .UseInMemoryDatabase($"EntWrite_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new KTransportDbContext(opts, new StubTenantContext());
    }

    private static EntitlementsService NewService(KTransportDbContext ctx)
        => new EntitlementsService(ctx, NullLogger<EntitlementsService>.Instance);

    /// <summary>Seeds the production modules + permissions catalogs with proper
    /// ModuleId FKs, so the structural writer finds every permission.</summary>
    private static async Task SeedProductionCatalog(KTransportDbContext ctx)
    {
        var modules = new (int Id, string Code)[]
        {
            (1, "dashboard"),
            (2, "bilty"),
            (3, "pod"),
            (4, "trips"),
            (5, "billing"),
            (6, "master_data"),
            (7, "reports"),
            (8, "vendors"),
            (9, "claims"),
            (10, "quotations"),
            (11, "tracking"),
            (12, "analytics"),
            (13, "system"),
            (14, "trip_settlement"),
            (15, "delivery_settlement"),
            (16, "clients"),
        };
        foreach (var (id, code) in modules)
        {
            ctx.Modules.Add(new Module { Id = id, Code = code, Name = code, IsActive = true });
        }

        // Match the real feature-key → module mapping used by the migration.
        string ResolveModule(string featureKey)
        {
            var k = featureKey.ToLowerInvariant();
            if (k == "dashboard") return "dashboard";
            if (k == "analytics") return "analytics";
            if (k == "tracking") return "tracking";
            if (k == "consignments" || k.StartsWith("consignments.") || k == "bilty" || k.StartsWith("bilty.")) return "bilty";
            if (k == "delivery_settlement" || k.StartsWith("delivery_settlement.")) return "delivery_settlement";
            if (k == "trip_settlement" || k.StartsWith("trip_settlement.")) return "trip_settlement";
            if (k == "empty_trips" || k.StartsWith("empty_trips.") || k == "trips" || k.StartsWith("trips.")) return "trips";
            if (k == "pod" || k.StartsWith("pod.")) return "pod";
            if (k == "billing" || k.StartsWith("billing.") || k == "bill_book") return "billing";
            if (k == "master_data" || k.StartsWith("master_data.")) return "master_data";
            if (k == "reports" || k.StartsWith("reports.")) return "reports";
            if (k == "vendors" || k.StartsWith("vendors.")) return "vendors";
            if (k == "claims" || k.StartsWith("claims.")) return "claims";
            if (k == "quotations" || k.StartsWith("quotations.")) return "quotations";
            if (k == "clients" || k.StartsWith("saas") || k.StartsWith("clients.")) return "clients";
            return "system";
        }

        var moduleIdByCode = modules.ToDictionary(m => m.Code, m => m.Id, StringComparer.OrdinalIgnoreCase);
        int permId = 100;
        foreach (var f in EntitlementsCatalog.Features)
        {
            foreach (var a in f.Actions)
            {
                var code = ResolveModule(f.FeatureKey);
                ctx.Permissions.Add(new Permission
                {
                    Id = permId++,
                    Key = $"{f.FeatureKey}.{a.ToLowerInvariant()}",
                    FeatureKey = f.FeatureKey,
                    Action = a,
                    ModuleId = moduleIdByCode[code]
                });
            }
        }
        ctx.Tenants.Add(new Tenant { Id = TestTenantId, Name = "Test", Code = "TEST", IsActive = true, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
    }

    // =================================================================
    // Central regression — menu.md §2 bug 3 ("isParentAllowed" deleted).
    // =================================================================

    [Fact]
    public async Task Assign_consignments_only_does_not_grant_delivery_settlement()
    {
        await using var ctx = NewContext();
        await SeedProductionCatalog(ctx);

        var svc = NewService(ctx);
        await svc.WriteTenantEntitlementsAsync(TestTenantId, new TenantMenuEntitlementsDto
        {
            TenantId = TestTenantId,
            ModuleCodes = new List<string> { "bilty" }
        }, userId: 1);

        var adminGrants = await ctx.RolePermissions.IgnoreQueryFilters()
            .Where(r => r.TenantId == TestTenantId && r.RoleName == "admin" && r.RevokedAt == null)
            .Select(r => r.PermissionKey)
            .ToListAsync();

        Assert.Contains(adminGrants, k => k.StartsWith("consignments.", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(adminGrants, k => k.StartsWith("delivery_settlement.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assign_consignments_and_delivery_settlement_grants_both()
    {
        await using var ctx = NewContext();
        await SeedProductionCatalog(ctx);

        var svc = NewService(ctx);
        await svc.WriteTenantEntitlementsAsync(TestTenantId, new TenantMenuEntitlementsDto
        {
            TenantId = TestTenantId,
            ModuleCodes = new List<string> { "bilty", "delivery_settlement" }
        }, userId: 1);

        var adminGrants = await ctx.RolePermissions.IgnoreQueryFilters()
            .Where(r => r.TenantId == TestTenantId && r.RoleName == "admin" && r.RevokedAt == null)
            .Select(r => r.PermissionKey)
            .ToListAsync();

        Assert.Contains(adminGrants, k => k.StartsWith("consignments.", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(adminGrants, k => k.StartsWith("delivery_settlement.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Revoke_delivery_settlement_marks_role_permissions_revoked()
    {
        await using var ctx = NewContext();
        await SeedProductionCatalog(ctx);

        var svc = NewService(ctx);
        // 1. Assign both.
        await svc.WriteTenantEntitlementsAsync(TestTenantId, new TenantMenuEntitlementsDto
        {
            TenantId = TestTenantId,
            ModuleCodes = new List<string> { "bilty", "delivery_settlement" }
        }, userId: 1);

        var afterAssign = await ctx.RolePermissions.IgnoreQueryFilters()
            .Where(r => r.TenantId == TestTenantId && r.RoleName == "admin"
                     && r.PermissionKey.StartsWith("delivery_settlement."))
            .ToListAsync();
        Assert.NotEmpty(afterAssign);
        Assert.All(afterAssign, r => Assert.Null(r.RevokedAt));

        // 2. Revoke delivery_settlement.
        await svc.WriteTenantEntitlementsAsync(TestTenantId, new TenantMenuEntitlementsDto
        {
            TenantId = TestTenantId,
            ModuleCodes = new List<string> { "bilty" }
        }, userId: 1);

        var afterRevoke = await ctx.RolePermissions.IgnoreQueryFilters()
            .Where(r => r.TenantId == TestTenantId && r.RoleName == "admin"
                     && r.PermissionKey.StartsWith("delivery_settlement."))
            .ToListAsync();
        Assert.NotEmpty(afterRevoke);
        Assert.All(afterRevoke, r => Assert.NotNull(r.RevokedAt));

        // tenant_modules row for delivery_settlement is append-only revoked.
        var dsTenantModule = await ctx.TenantModules.IgnoreQueryFilters()
            .Join(ctx.Modules.IgnoreQueryFilters(), tm => tm.ModuleId, m => m.Id, (tm, m) => new { tm, m })
            .Where(x => x.tm.TenantId == TestTenantId && x.m.Code == "delivery_settlement")
            .Select(x => x.tm)
            .FirstAsync();
        Assert.NotNull(dsTenantModule.EnabledUntil);
    }

    [Fact]
    public async Task Enabled_feature_keys_mirror_matches_tenant_modules_after_write()
    {
        await using var ctx = NewContext();
        await SeedProductionCatalog(ctx);

        var svc = NewService(ctx);
        await svc.WriteTenantEntitlementsAsync(TestTenantId, new TenantMenuEntitlementsDto
        {
            TenantId = TestTenantId,
            ModuleCodes = new List<string> { "bilty", "pod", "billing" }
        }, userId: 1);

        var activeModules = await ctx.TenantModules.IgnoreQueryFilters()
            .Where(tm => tm.TenantId == TestTenantId && tm.EnabledUntil == null)
            .Join(ctx.Modules.IgnoreQueryFilters(), tm => tm.ModuleId, m => m.Id, (tm, m) => m.Code)
            .OrderBy(c => c)
            .ToListAsync();

        var sub = await ctx.TenantEntitlementSubscriptions.IgnoreQueryFilters()
            .FirstAsync(s => s.TenantId == TestTenantId && s.EffectiveUntil == null);
        var mirror = sub.EnabledFeatureKeys.OrderBy(c => c).ToList();

        Assert.Equal(activeModules, mirror);
    }
}
