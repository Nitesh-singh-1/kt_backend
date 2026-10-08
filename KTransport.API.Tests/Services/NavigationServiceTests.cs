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
/// TASK-039 permission sweep tests, updated for TASK-044 Phase 3 — entitlements
/// live in normalized tables only.
/// </summary>
public class NavigationServiceTests
{
    private static readonly Guid TestTenantId = TenantConstants.DefaultTenantId;
    // TASK-045 Phase 3: tests exercise sub-user visibility (not super-user).
    // Super-users now get a '*' wildcard that bypasses per-row permission
    // checks in BuildMenuFromTablesAsync, so a non-super-user role is used to
    // keep the fine-grained visibility assertions meaningful.
    private const string AdminRole = "accountant";
    private const string AdminId = "accountant";

    private sealed class StubTenantContext : ITenantContext
    {
        public Guid CurrentTenantId { get; private set; } = TestTenantId;
        public bool HasTenant => true;
        public void SetTenantId(Guid tenantId) => CurrentTenantId = tenantId;
    }

    private static KTransportDbContext NewContext()
    {
        var opts = new DbContextOptionsBuilder<KTransportDbContext>()
            .UseInMemoryDatabase(databaseName: $"NavigationServiceTests_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new KTransportDbContext(opts, new StubTenantContext());
    }

    private static void SeedTenantEntitlements(
        KTransportDbContext ctx,
        List<string> enabledMenuKeys,
        List<ReportEntitlementItemDto>? reports = null)
    {
        ctx.TenantSettings.Add(new TenantSetting { TenantId = TestTenantId, CreatedAt = DateTime.UtcNow });
        ctx.TenantEntitlementSubscriptions.Add(new TenantEntitlementSubscription
        {
            TenantId = TestTenantId,
            PlanTier = "Custom",
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

    private static DynamicMenuItemDto? FindMenu(IEnumerable<DynamicMenuItemDto> menu, string id)
        => menu.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// TASK-045 Phase 3: GetDynamicMenuAsync is table-driven. Tests must seed
    /// the menu_items catalog so BuildMenuFromTablesAsync has rows to filter.
    /// </summary>
    private static void ApplyMenuSeed(KTransportDbContext ctx)
    {
        var existing = ctx.MenuItems.Select(m => m.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
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
        }
        ctx.SaveChanges();
    }

    [Fact]
    public async Task NavigationService_BillBookChild_NotEmittedWhenPageKeyMissing()
    {
        await using var ctx = NewContext();
        ApplyMenuSeed(ctx);
        SeedTenantEntitlements(ctx, new List<string>
        {
            "dashboard",
            "billing",
            "billing.invoices"
        });

        var service = new NavigationService(ctx);
        var menu = await service.GetDynamicMenuAsync(TestTenantId, AdminRole, AdminId);
        var permissions = await service.GetUserPermissionsAsync(TestTenantId, AdminRole, AdminId);

        var billing = FindMenu(menu, "billing");
        Assert.NotNull(billing);
        Assert.NotNull(billing!.Children);

        Assert.DoesNotContain(billing.Children, c => c.Id == "billing.bill_book");
        Assert.DoesNotContain("billing.bill_book", permissions, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(billing.Children, c => c.Id == "billing.invoices");
    }

    [Fact]
    public async Task NavigationService_BillBookChild_EmittedWhenPageKeyPresent()
    {
        await using var ctx = NewContext();
        ApplyMenuSeed(ctx);
        SeedTenantEntitlements(ctx, new List<string>
        {
            "dashboard",
            "billing",
            "billing.bill_book"
        });

        var service = new NavigationService(ctx);
        var menu = await service.GetDynamicMenuAsync(TestTenantId, AdminRole, AdminId);
        var permissions = await service.GetUserPermissionsAsync(TestTenantId, AdminRole, AdminId);

        var billing = FindMenu(menu, "billing");
        Assert.NotNull(billing);
        Assert.NotNull(billing!.Children);
        Assert.Contains(billing.Children, c => c.Id == "billing.bill_book");
        Assert.Contains("billing.bill_book", permissions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReportsSweep_HiddenReportKey_StaysOutOfPermissions()
    {
        await using var ctx = NewContext();
        var reports = new List<ReportEntitlementItemDto>
        {
            new() { ReportKey = "booking_register", Title = "Consignment Booking Register", Category = "Operational", Path = "/reports?tab=booking_register", IsEnabled = true },
            new() { ReportKey = "vendor_payables", Title = "Vendor Payables", Category = "Financial", Path = "/reports?tab=vendor_payables", IsEnabled = true }
        };
        ApplyMenuSeed(ctx);
        SeedTenantEntitlements(ctx, new List<string>
        {
            "dashboard",
            "reports",
            "reports.vendor_payables"
        }, reports);

        var service = new NavigationService(ctx);
        var menu = await service.GetDynamicMenuAsync(TestTenantId, AdminRole, AdminId);
        var permissions = await service.GetUserPermissionsAsync(TestTenantId, AdminRole, AdminId);

        var reportsMenu = FindMenu(menu, "reports");
        Assert.NotNull(reportsMenu);

        Assert.DoesNotContain(reportsMenu!.Children ?? new List<DynamicMenuItemDto>(),
            c => c.Id == "reports.booking_register");
        Assert.DoesNotContain("reports.booking_register", permissions, StringComparer.OrdinalIgnoreCase);

        Assert.Contains(reportsMenu.Children ?? new List<DynamicMenuItemDto>(),
            c => c.Id == "reports.vendor_payables");
        Assert.Contains("reports.vendor_payables", permissions, StringComparer.OrdinalIgnoreCase);
    }
}
