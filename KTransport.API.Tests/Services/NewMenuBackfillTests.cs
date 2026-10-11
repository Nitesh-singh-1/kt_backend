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
/// TASK-049 Option B — "future menu" regression. The owner's explicit
/// architectural requirement is that adding a brand-new module (its
/// <c>modules</c> row + <c>menu_items</c> row + <c>permissions</c> rows) and
/// assigning it to a tenant via <see cref="EntitlementsService.WriteTenantEntitlementsAsync"/>
/// makes the module appear in the sidebar WITHOUT any code change. This test
/// proves the structural rewrite scales — the module/menu catalog is data,
/// not code.
/// </summary>
public class NewMenuBackfillTests
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
            .UseInMemoryDatabase($"NewMenu_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new KTransportDbContext(opts, new StubTenantContext());
    }

    [Fact]
    public async Task New_module_fuel_log_shows_in_menu_after_assign_with_no_code_change()
    {
        await using var ctx = NewContext();

        // 1. Seed the base production-ish catalog so the dashboard anchor exists.
        ctx.Modules.Add(new Module { Id = 1, Code = "dashboard", Name = "Dashboard", IsActive = true });
        ctx.Modules.Add(new Module { Id = 13, Code = "system",    Name = "System",    IsActive = true });
        ctx.Permissions.Add(new Permission { Id = 10, Key = "dashboard.view", FeatureKey = "dashboard", Action = "View", ModuleId = 1 });
        ctx.Permissions.Add(new Permission { Id = 11, Key = "system.view",    FeatureKey = "system",    Action = "View", ModuleId = 13 });

        ctx.MenuItems.Add(new MenuItem
        {
            Key = "dashboard", Title = "Dashboard", Path = "/dashboard", Icon = "home",
            PermissionKey = "dashboard.view", DisplayOrder = 0, IsActive = true, ModuleId = 1
        });

        // 2. Add a BRAND-NEW module `fuel_log` to the catalog — simulating the
        //    future-menu workflow where a platform-admin inserts a new module
        //    row + menu item + permission rows.
        ctx.Modules.Add(new Module { Id = 50, Code = "fuel_log", Name = "Fuel Log", IsActive = true, DisplayOrder = 50 });
        ctx.Permissions.Add(new Permission { Id = 500, Key = "fuel_log.view", FeatureKey = "fuel_log", Action = "View", ModuleId = 50 });
        ctx.MenuItems.Add(new MenuItem
        {
            Key = "fuel_log", Title = "Fuel Log", Path = "/fuel-log", Icon = "truck",
            PermissionKey = "fuel_log.view", DisplayOrder = 50, IsActive = true, ModuleId = 50
        });

        ctx.Tenants.Add(new Tenant { Id = TestTenantId, Name = "Test", Code = "TEST", IsActive = true, CreatedAt = DateTime.UtcNow });

        var adminUser = new User
        {
            TenantId = TestTenantId, Username = "fuel_admin", Password = "x",
            FullName = "Fuel Admin", Role = "admin", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.Users.Add(adminUser);
        await ctx.SaveChangesAsync();

        var ent = new EntitlementsService(ctx, NullLogger<EntitlementsService>.Instance);
        var nav = new NavigationService(ctx, ent, NullLogger<NavigationService>.Instance);

        // 3. Platform-admin assigns the new module to the tenant — SAME API
        //    used for every other module, no custom code path.
        await ent.WriteTenantEntitlementsAsync(TestTenantId, new TenantMenuEntitlementsDto
        {
            TenantId = TestTenantId,
            ModuleCodes = new List<string> { "fuel_log" }
        }, userId: 1);

        // 4. The sidebar for the admin user must now contain `fuel_log`.
        var menu = await nav.GetDynamicMenuAsync(TestTenantId, "admin", adminUser.Username);
        var ids = menu.Select(m => m.Id).ToList();
        Assert.Contains("fuel_log", ids);

        // 5. The admin role should have been seeded with fuel_log.view
        //    purely from the structural join — no code change required.
        var adminGrants = await ctx.RolePermissions.IgnoreQueryFilters()
            .Where(r => r.TenantId == TestTenantId && r.RoleName == "admin" && r.RevokedAt == null)
            .Select(r => r.PermissionKey)
            .ToListAsync();
        Assert.Contains("fuel_log.view", adminGrants);
    }
}
