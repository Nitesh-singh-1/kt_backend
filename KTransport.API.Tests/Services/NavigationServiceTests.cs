using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KTransport.API.Tests.Services;

/// <summary>
/// TASK-039 — permission sweep tests.
///
/// Covers the three "tests_required" bullets in
/// <c>.agent/contracts/TASK-039-access-control-hardening.yaml</c> that live on the
/// NavigationService layer:
///   NavigationService_BillBookChild_NotEmittedWhenPageKeyMissing
///   NavigationService_BillBookChild_EmittedWhenPageKeyPresent
///   ReportsSweep_HiddenReportKey_StaysOutOfPermissions
///
/// Uses the EF InMemory provider in the same shape as
/// <see cref="InvoiceServiceTests"/> and <see cref="TripSettlementServiceTests"/>.
/// </summary>
public class NavigationServiceTests
{
    private static readonly Guid TestTenantId = TenantConstants.DefaultTenantId;
    private const string AdminRole = "admin"; // super-user role per NavigationService
    private const string AdminId = "admin";

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
        var dto = new TenantMenuEntitlementsDto
        {
            TenantId = TestTenantId,
            PlanTier = "Custom",
            EnabledMenuKeys = enabledMenuKeys,
            Reports = reports ?? new List<ReportEntitlementItemDto>()
        };

        ctx.TenantSettings.Add(new TenantSetting
        {
            TenantId = TestTenantId,
            MenuEntitlementsJson = JsonSerializer.Serialize(dto, JsonOpts),
            CreatedAt = DateTime.UtcNow
        });
        ctx.SaveChanges();
    }

    private static DynamicMenuItemDto? FindMenu(IEnumerable<DynamicMenuItemDto> menu, string id)
        => menu.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));

    // ---------- 1. Bill Book child NOT emitted when per-page key is missing ----------
    [Fact]
    public async Task NavigationService_BillBookChild_NotEmittedWhenPageKeyMissing()
    {
        await using var ctx = NewContext();
        // Tenant has the Billing MODULE but NOT the `billing.bill_book` page key.
        SeedTenantEntitlements(ctx, new List<string>
        {
            "dashboard",
            "billing",
            "billing.invoices" // another sibling to prove the parent still opens for other children
        });

        var service = new NavigationService(ctx);
        var menu = await service.GetDynamicMenuAsync(TestTenantId, AdminRole, AdminId);
        var permissions = await service.GetUserPermissionsAsync(TestTenantId, AdminRole, AdminId);

        var billing = FindMenu(menu, "billing");
        Assert.NotNull(billing);
        Assert.NotNull(billing!.Children);

        // The parent is open (user has `billing`), but the bill_book child is withheld.
        Assert.DoesNotContain(billing.Children, c => c.Id == "billing.bill_book");

        // And the permissions endpoint must not leak the granular page key either.
        Assert.DoesNotContain("billing.bill_book", permissions, StringComparer.OrdinalIgnoreCase);

        // Sanity: siblings granted by their own granular key still render.
        Assert.Contains(billing.Children, c => c.Id == "billing.invoices");
    }

    // ---------- 2. Bill Book child IS emitted when per-page key is present ----------
    [Fact]
    public async Task NavigationService_BillBookChild_EmittedWhenPageKeyPresent()
    {
        await using var ctx = NewContext();
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

    // ---------- 3. Reports sweep: hidden report tabs stay out of permissions ----------
    [Fact]
    public async Task ReportsSweep_HiddenReportKey_StaysOutOfPermissions()
    {
        await using var ctx = NewContext();
        // Tenant subscribes to the Reports module and ONE report (vendor_payables).
        // booking_register is a tenant-catalog entry with IsEnabled=true BUT the user
        // does not have `reports.booking_register` in their granted keys. The sweep
        // must drop it from both the menu and the permissions list.
        var reports = new List<ReportEntitlementItemDto>
        {
            new()
            {
                ReportKey = "booking_register",
                Title = "Consignment Booking Register",
                Category = "Operational",
                Path = "/reports?tab=booking_register",
                IsEnabled = true
            },
            new()
            {
                ReportKey = "vendor_payables",
                Title = "Vendor Payables",
                Category = "Financial",
                Path = "/reports?tab=vendor_payables",
                IsEnabled = true
            }
        };
        SeedTenantEntitlements(ctx, new List<string>
        {
            "dashboard",
            "reports",
            "reports.vendor_payables"
            // Deliberately NOT "reports.booking_register" — it is in the catalog with
            // IsEnabled=true but was never granted at the key level.
        }, reports);

        var service = new NavigationService(ctx);
        var menu = await service.GetDynamicMenuAsync(TestTenantId, AdminRole, AdminId);
        var permissions = await service.GetUserPermissionsAsync(TestTenantId, AdminRole, AdminId);

        var reportsMenu = FindMenu(menu, "reports");
        Assert.NotNull(reportsMenu);

        // The hidden report must not appear as a tab.
        Assert.DoesNotContain(reportsMenu!.Children ?? new List<DynamicMenuItemDto>(),
            c => c.Id == "reports.booking_register");
        // And it must not leak into the permissions endpoint.
        Assert.DoesNotContain("reports.booking_register", permissions, StringComparer.OrdinalIgnoreCase);

        // The granted sibling still renders and leaks correctly.
        Assert.Contains(reportsMenu.Children ?? new List<DynamicMenuItemDto>(),
            c => c.Id == "reports.vendor_payables");
        Assert.Contains("reports.vendor_payables", permissions, StringComparer.OrdinalIgnoreCase);
    }
}
