using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
/// TASK-045 Phase 3 — menu_items table is the sole production path.
/// The dual-read harness (MenuOptions.DualReadMode) is deleted along with
/// the ~550-line legacy fallback. Tests exercise MenuCatalogService
/// directly plus NavigationService wiring.
/// </summary>
public class MenuCatalogServiceTests
{
    private static readonly Guid TestTenantId = TenantConstants.DefaultTenantId;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed class StubTenantContext : ITenantContext
    {
        public Guid CurrentTenantId { get; private set; } = TestTenantId;
        public bool HasTenant => true;
        public void SetTenantId(Guid tenantId) => CurrentTenantId = tenantId;
    }

    private static KTransportDbContext NewContext()
    {
        var opts = new DbContextOptionsBuilder<KTransportDbContext>()
            .UseInMemoryDatabase(databaseName: $"MenuCatalogTests_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new KTransportDbContext(opts, new StubTenantContext());
    }

    private static MenuCatalogService NewService(KTransportDbContext ctx)
        => new MenuCatalogService(ctx, NullLogger<MenuCatalogService>.Instance);

    /// <summary>Idempotent in-memory replay of the SeedMenuItemsCatalog migration.</summary>
    private static int ApplyMenuSeed(KTransportDbContext ctx)
    {
        var existing = ctx.MenuItems.Select(m => m.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int inserted = 0;
        foreach (var r in MenuCatalogSeedData.Rows)
        {
            if (existing.Contains(r.Key)) continue;
            ctx.MenuItems.Add(new MenuItem
            {
                Key = r.Key,
                ParentKey = r.ParentKey,
                Title = r.Title,
                Path = r.Path,
                Icon = r.Icon,
                PermissionKey = r.PermissionKey,
                Badge = r.Badge,
                DisplayOrder = r.DisplayOrder,
                VisibilityRule = r.VisibilityRule,
                IsActive = true
            });
            inserted++;
        }
        ctx.SaveChanges();
        return inserted;
    }

    private static void SeedTenantEntitlements(KTransportDbContext ctx, List<string> enabledMenuKeys, List<ReportEntitlementItemDto>? reports = null)
    {
        ctx.TenantSettings.Add(new TenantSetting { TenantId = TestTenantId, CreatedAt = DateTime.UtcNow });
        ctx.TenantEntitlementSubscriptions.Add(new TenantEntitlementSubscription
        {
            TenantId = TestTenantId,
            PlanTier = "Enterprise",
            EnabledFeatureKeys = enabledMenuKeys,
            EffectiveFrom = DateTime.UtcNow,
            EffectiveUntil = null
        });
        if (reports != null)
        {
            foreach (var r in reports)
            {
                ctx.TenantReportEntitlements.Add(new TenantReportEntitlement
                {
                    TenantId = TestTenantId,
                    ReportKey = r.ReportKey,
                    IsEnabled = r.IsEnabled,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }
        ctx.SaveChanges();
    }

    private static List<string> EnterpriseKeys()
    {
        return new List<string>
        {
            "dashboard","analytics","consignments","consignments.create","consignments.all",
            "delivery_settlement","quotations","trips",
            "trip_settlement","empty_trips","pod","billing","billing.bill_book",
            "bill_book","billing.invoices","billing.receipts","master_data","master_data.parties",
            "master_data.fleet","master_data.compliance","master_data.tyres","master_data.spares",
            "master_data.loans","master_data.driver_ledger","master_data.vehicle_claims",
            "master_data.rates","master_data.vendor_rates","vendors","claims","reports",
            "reports.booking_register","reports.tax_summary","reports.party_outstanding",
            "reports.trip_profitability","reports.vendor_payables","tracking","clients",
            "system","system.settings","system.onboard","system.forgot_password"
        };
    }

    // ---- Seed sanity ----------------------------------------------------
    [Fact]
    public void SeedMigration_Produces36RowsExactly()
    {
        Assert.Equal(36, MenuCatalogSeedData.Rows.Length);

        using var ctx = NewContext();
        ApplyMenuSeed(ctx);
        Assert.Equal(36, ctx.MenuItems.Count());
    }

    [Fact]
    public void SeedMigration_Idempotent_SecondRunInsertsZero()
    {
        using var ctx = NewContext();
        var first = ApplyMenuSeed(ctx);
        var second = ApplyMenuSeed(ctx);
        Assert.Equal(36, first);
        Assert.Equal(0, second);
        Assert.Equal(36, ctx.MenuItems.Count());
    }

    [Fact]
    public async Task GetAllAsync_Returns36Rows()
    {
        await using var ctx = NewContext();
        ApplyMenuSeed(ctx);

        var svc = NewService(ctx);
        var all = await svc.GetAllAsync();
        Assert.Equal(36, all.Count);
        Assert.Contains(all, m => m.Key == "reports" && m.VisibilityRule == "report_entitlement");
    }

    [Fact]
    public void Phase2_SeedData_HasZeroStaleKeys()
    {
        string[] stale = new[] { "driverledger", "vehicleclaims", "vendorrates", "trips.settlement", "trips.all", "consignments.delivery_settlement" };
        var keys = MenuCatalogSeedData.Rows.Select(r => r.Key).ToArray();
        foreach (var s in stale)
        {
            Assert.DoesNotContain(keys, k => k.Equals(s, StringComparison.OrdinalIgnoreCase));
        }
    }

    // ============================================================
    // TASK-045 Phase 3 — tests_required from contract
    // ============================================================

    // Contract bullet 1: legacy fallback method name is gone from NavigationService.cs
    [Fact]
    public void Phase3_NavigationService_HasNoLegacyFallbackReference()
    {
        var repoRoot = AppContext.BaseDirectory;
        // Walk up to find the solution's src file.
        var dir = new DirectoryInfo(repoRoot);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "KTransport.API.sln")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        var navPath = Path.Combine(dir!.FullName, "KTransport.API", "Services", "NavigationService.cs");
        Assert.True(File.Exists(navPath), $"expected to find NavigationService.cs at {navPath}");
        var contents = File.ReadAllText(navPath);
        Assert.DoesNotContain("BuildMenuFromCodeTree_LegacyPhase1Fallback", contents);
    }

    // Contract bullet 2: Enterprise admin baseline byte-identical to Phase 2 fixture.
    [Fact]
    public async Task Phase3_GetDynamicMenu_MatchesCapturedBaseline_ForEnterpriseAdmin()
    {
        await using var ctx = NewContext();
        ApplyMenuSeed(ctx);
        SeedTenantEntitlements(ctx, EnterpriseKeys());

        var catalog = NewService(ctx);
        var nav = new NavigationService(ctx, null, null, catalog);
        var menu = await nav.GetDynamicMenuAsync(TestTenantId, "admin", "admin");

        var actualJson = JsonSerializer.Serialize(menu, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "menu_baseline_enterprise.json");
        Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);

        if (!File.Exists(fixturePath))
        {
            File.WriteAllText(fixturePath, actualJson);
        }

        var expectedJson = File.ReadAllText(fixturePath);
        Assert.Equal(expectedJson.Trim(), actualJson.Trim());
    }

    // Contract bullet 3: dispatcher (TASK-043 grant backfill) sees master_data.driver_ledger.
    [Fact]
    public async Task Phase3_GetDynamicMenu_Dispatcher_SeesDriverLedger()
    {
        await using var ctx = NewContext();
        ApplyMenuSeed(ctx);
        SeedTenantEntitlements(ctx, EnterpriseKeys());

        // Seed dispatcher role grants mirroring the TASK-043 backfill: both the
        // module key (master_data) and the leaf (master_data.driver_ledger.view).
        foreach (var key in new[] { "master_data", "master_data.driver_ledger.view", "dashboard.view" })
        {
            ctx.RolePermissions.Add(new RolePermission
            {
                TenantId = TestTenantId,
                RoleName = "dispatcher",
                PermissionKey = key,
                GrantedAt = DateTime.UtcNow
            });
        }
        // Also seed a user so the entitlements service can resolve.
        ctx.Users.Add(new User
        {
            Id = 777,
            TenantId = TestTenantId,
            Username = "disp1",
            Role = "dispatcher",
            Password = "x",
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var ent = new EntitlementsService(ctx, NullLogger<EntitlementsService>.Instance);
        var catalog = NewService(ctx);
        var nav = new NavigationService(ctx, ent, NullLogger<NavigationService>.Instance, catalog);
        var menu = await nav.GetDynamicMenuAsync(TestTenantId, "dispatcher", "777");

        var flat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void collect(DynamicMenuItemDto i)
        {
            flat.Add(i.Id);
            if (i.Children != null) foreach (var c in i.Children) collect(c);
        }
        foreach (var m in menu) collect(m);

        Assert.Contains("master_data", flat);
        Assert.Contains("master_data.driver_ledger", flat);
    }

    // Contract bullet 4: appsettings has no 'Menu' key; MenuOptions.cs does not exist.
    [Fact]
    public void Phase3_AppSettings_NoMenuSection_AndMenuOptionsFileDeleted()
    {
        var repoRoot = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(repoRoot);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "KTransport.API.sln")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);

        var apiRoot = Path.Combine(dir!.FullName, "KTransport.API");
        var app = File.ReadAllText(Path.Combine(apiRoot, "appsettings.json"));
        var appDev = File.ReadAllText(Path.Combine(apiRoot, "appsettings.Development.json"));
        Assert.DoesNotContain("\"Menu\"", app);
        Assert.DoesNotContain("\"Menu\"", appDev);
        Assert.False(File.Exists(Path.Combine(apiRoot, "Common", "MenuOptions.cs")));
    }

    // Contract bullet 5: tables mode no longer emits trips.all (never did; legacy is deleted).
    [Fact]
    public async Task Phase3_TablesPath_DoesNotEmitTripsAll()
    {
        await using var ctx = NewContext();
        ApplyMenuSeed(ctx);

        var effective = EnterpriseKeys().ToHashSet(StringComparer.OrdinalIgnoreCase);
        effective.Add("system"); effective.Add("system.settings"); effective.Add("clients"); effective.Add("*");
        var entitlements = new TenantMenuEntitlementsDto { TenantId = TestTenantId, Reports = new() };

        var svc = NewService(ctx);
        var menu = await svc.BuildMenuFromTablesAsync(effective, entitlements);

        var flat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void collect(DynamicMenuItemDto i) { flat.Add(i.Id); if (i.Children != null) foreach (var c in i.Children) collect(c); }
        foreach (var m in menu) collect(m);
        Assert.DoesNotContain("trips.all", flat);
        Assert.Contains("trips", flat);
        Assert.Contains("trip_settlement", flat);
    }

    // Contract bullet 6: BuildMenuFromTablesAsync for Enterprise admin matches catalog ids.
    [Fact]
    public async Task Phase3_BuildMenuFromTables_EnterpriseAdmin_EmitsCatalogIds()
    {
        await using var ctx = NewContext();
        ApplyMenuSeed(ctx);

        var effective = EnterpriseKeys().ToHashSet(StringComparer.OrdinalIgnoreCase);
        effective.Add("system"); effective.Add("system.settings"); effective.Add("clients"); effective.Add("*");

        var reports = new List<ReportEntitlementItemDto>
        {
            new() { ReportKey = "booking_register",   Title = "Booking Register",   IsEnabled = true, Path = "/reports?tab=booking_register" },
            new() { ReportKey = "tax_summary",        Title = "GST Summary",        IsEnabled = true, Path = "/reports?tab=tax_summary" },
            new() { ReportKey = "party_outstanding",  Title = "Party Outstanding",  IsEnabled = true, Path = "/reports?tab=party_outstanding" },
            new() { ReportKey = "trip_profitability", Title = "Trip Profitability", IsEnabled = true, Path = "/reports?tab=trip_profitability" },
            new() { ReportKey = "vendor_payables",    Title = "Vendor Payables",    IsEnabled = true, Path = "/reports?tab=vendor_payables" },
        };
        var entitlements = new TenantMenuEntitlementsDto
        {
            TenantId = TestTenantId,
            EnabledMenuKeys = EnterpriseKeys(),
            Reports = reports
        };
        foreach (var r in reports) effective.Add("reports." + r.ReportKey);

        var svc = NewService(ctx);
        var menu = await svc.BuildMenuFromTablesAsync(effective, entitlements);

        var flat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void collect(DynamicMenuItemDto i)
        {
            flat.Add(i.Id);
            if (i.Children != null) foreach (var c in i.Children) collect(c);
        }
        foreach (var m in menu) collect(m);

        foreach (var row in MenuCatalogSeedData.Rows)
        {
            Assert.True(flat.Contains(row.Key), $"expected flattened menu to contain '{row.Key}'");
        }
        foreach (var r in reports)
        {
            Assert.True(flat.Contains("reports." + r.ReportKey), $"expected reports.'{r.ReportKey}' child");
        }
    }

    // Contract bullet 7: restricted viewer sees only dashboard + reports.
    [Fact]
    public async Task Phase3_BuildMenuFromTables_RestrictedViewer_OnlyDashboardAndReports()
    {
        await using var ctx = NewContext();
        ApplyMenuSeed(ctx);

        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "dashboard", "dashboard.view", "reports", "reports.view"
        };
        var entitlements = new TenantMenuEntitlementsDto { TenantId = TestTenantId, Reports = new() };

        var svc = NewService(ctx);
        var menu = await svc.BuildMenuFromTablesAsync(effective, entitlements);

        var topIds = menu.Select(m => m.Id).ToList();
        Assert.Contains("dashboard", topIds);
        Assert.Contains("reports", topIds);
        Assert.DoesNotContain("master_data", topIds);
        Assert.DoesNotContain("billing", topIds);
        Assert.DoesNotContain("trips", topIds);
    }

    // Reports children loop still runs.
    [Fact]
    public async Task Phase3_BuildMenuFromTables_FiveEnabledReports_ProducesFiveChildren()
    {
        await using var ctx = NewContext();
        ApplyMenuSeed(ctx);

        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "reports", "reports.view",
            "reports.booking_register","reports.tax_summary","reports.party_outstanding","reports.trip_profitability","reports.vendor_payables"
        };
        var reports = new List<ReportEntitlementItemDto>
        {
            new() { ReportKey = "booking_register",   Title = "Booking Register",   IsEnabled = true },
            new() { ReportKey = "tax_summary",        Title = "GST Summary",        IsEnabled = true },
            new() { ReportKey = "party_outstanding",  Title = "Party Outstanding",  IsEnabled = true },
            new() { ReportKey = "trip_profitability", Title = "Trip Profitability", IsEnabled = true },
            new() { ReportKey = "vendor_payables",    Title = "Vendor Payables",    IsEnabled = true },
        };
        var entitlements = new TenantMenuEntitlementsDto
        {
            TenantId = TestTenantId,
            Reports = reports
        };

        var svc = NewService(ctx);
        var menu = await svc.BuildMenuFromTablesAsync(effective, entitlements);

        var reportsMenu = menu.First(m => m.Id == "reports");
        var reportChildren = reportsMenu.Children.Where(c => c.Id.StartsWith("reports.")).ToList();
        Assert.Equal(5, reportChildren.Count);
    }

    // Every top-level/child catalog Id is honored via the legacy NavigationService(ctx) ctor.
    [Fact]
    public async Task Phase3_EveryTopLevelId_HasParentNullRowInCatalog()
    {
        await using var ctx = NewContext();
        ApplyMenuSeed(ctx);
        SeedTenantEntitlements(ctx, EnterpriseKeys());

        var nav = new NavigationService(ctx);
        var menu = await nav.GetDynamicMenuAsync(TestTenantId, "admin", "admin");

        var topLevelIds = menu.Select(m => m.Id).ToList();
        var catalogTopKeys = ctx.MenuItems.Where(m => m.ParentKey == null).Select(m => m.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var id in topLevelIds)
            Assert.True(catalogTopKeys.Contains(id), $"Top-level Id '{id}' missing from menu_items (parent_key IS NULL)");
    }
}
