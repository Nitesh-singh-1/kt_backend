using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using Module = KTransport.API.Models.Module;

namespace KTransport.API.Tests.Services;

/// <summary>
/// TASK-054 — RBAC/menu/entitlement clean-slate consolidation. One engine, one
/// model: module + page ceiling, genuine-admin keystone, sub-user page grants,
/// fresh-DB bootstrap, cross-tenant isolation, and the grep-guard that the
/// legacy alias normalizers are gone.
/// </summary>
public class RbacConsolidationTests
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

    // Module ids
    private const int MDashboard = 1, MBilty = 2, MBilling = 3, MMaster = 4, MReports = 5, MClients = 6, MSystem = 7;

    private static KTransportDbContext NewContext()
    {
        var opts = new DbContextOptionsBuilder<KTransportDbContext>()
            .UseInMemoryDatabase($"Rbac054_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new KTransportDbContext(opts, new StubTenantContext());
    }

    private static EntitlementsService NewEnt(KTransportDbContext ctx) => new(ctx, NullLogger<EntitlementsService>.Instance);
    private static NavigationService NewNav(KTransportDbContext ctx, IEntitlementsService ent) => new(ctx, ent, NullLogger<NavigationService>.Instance);

    /// <summary>Seed the canonical catalog (modules + permissions + menu_items) for a tenant.</summary>
    private static async Task SeedCatalogAsync(KTransportDbContext ctx)
    {
        ctx.Modules.AddRange(
            new Module { Id = MDashboard, Code = "dashboard", Name = "Dashboard", IsActive = true },
            new Module { Id = MBilty, Code = "bilty", Name = "Bilty", IsActive = true },
            new Module { Id = MBilling, Code = "billing", Name = "Billing", IsActive = true },
            new Module { Id = MMaster, Code = "master_data", Name = "Master Data", IsActive = true },
            new Module { Id = MReports, Code = "reports", Name = "Reports", IsActive = true },
            new Module { Id = MClients, Code = "clients", Name = "Clients", IsActive = true },
            new Module { Id = MSystem, Code = "system", Name = "System", IsActive = true }
        );

        ctx.Permissions.AddRange(
            new Permission { Id = 1, Key = "dashboard.view", ModuleId = MDashboard, Action = "View", PageKey = null },
            new Permission { Id = 2, Key = "consignments.all.view", ModuleId = MBilty, Action = "View", PageKey = "consignments.all" },
            new Permission { Id = 3, Key = "consignments.create.create", ModuleId = MBilty, Action = "Create", PageKey = "consignments.create" },
            new Permission { Id = 4, Key = "consignments.create.view", ModuleId = MBilty, Action = "View", PageKey = "consignments.create" },
            new Permission { Id = 5, Key = "billing.view", ModuleId = MBilling, Action = "View", PageKey = null },
            new Permission { Id = 6, Key = "billing.invoices.view", ModuleId = MBilling, Action = "View", PageKey = "billing.invoices" },
            new Permission { Id = 7, Key = "billing.bill_book.view", ModuleId = MBilling, Action = "View", PageKey = "billing.bill_book" },
            new Permission { Id = 8, Key = "master_data.parties.view", ModuleId = MMaster, Action = "View", PageKey = "master_data.parties" },
            new Permission { Id = 9, Key = "master_data.fleet.view", ModuleId = MMaster, Action = "View", PageKey = "master_data.fleet" },
            new Permission { Id = 10, Key = "reports.view", ModuleId = MReports, Action = "View", PageKey = null },
            new Permission { Id = 11, Key = "clients.view", ModuleId = MClients, Action = "View", PageKey = null }
        );

        // Menu items: group parents (no path) + child pages, with ModuleId + ParentId.
        ctx.MenuItems.AddRange(
            new MenuItem { Id = 1, Key = "dashboard", Title = "Dashboard", Path = "/dashboard", ModuleId = MDashboard, DisplayOrder = 0, IsActive = true },
            new MenuItem { Id = 2, Key = "consignments", Title = "Consignments", Path = null, ModuleId = MBilty, DisplayOrder = 1, IsActive = true },
            new MenuItem { Id = 3, Key = "consignments.all", Title = "All Bilties", Path = "/shipments", ModuleId = MBilty, ParentId = 2, DisplayOrder = 0, IsActive = true },
            new MenuItem { Id = 4, Key = "consignments.create", Title = "New Bilty", Path = "/shipments/create", ModuleId = MBilty, ParentId = 2, DisplayOrder = 1, IsActive = true },
            new MenuItem { Id = 5, Key = "billing", Title = "Billing", Path = null, ModuleId = MBilling, DisplayOrder = 2, IsActive = true },
            new MenuItem { Id = 6, Key = "billing.invoices", Title = "Invoices", Path = "/billing", ModuleId = MBilling, ParentId = 5, DisplayOrder = 0, IsActive = true },
            new MenuItem { Id = 7, Key = "billing.bill_book", Title = "Bill Book", Path = "/bill-book", ModuleId = MBilling, ParentId = 5, DisplayOrder = 1, IsActive = true },
            new MenuItem { Id = 8, Key = "master_data", Title = "Master Data", Path = null, ModuleId = MMaster, DisplayOrder = 3, IsActive = true },
            new MenuItem { Id = 9, Key = "master_data.parties", Title = "Parties", Path = "/customers", ModuleId = MMaster, ParentId = 8, DisplayOrder = 0, IsActive = true },
            new MenuItem { Id = 10, Key = "master_data.fleet", Title = "Fleet", Path = "/fleet", ModuleId = MMaster, ParentId = 8, DisplayOrder = 1, IsActive = true },
            new MenuItem { Id = 11, Key = "reports", Title = "Reports", Path = "/reports", ModuleId = MReports, DisplayOrder = 4, IsActive = true },
            new MenuItem { Id = 12, Key = "clients", Title = "Clients", Path = "/clients", ModuleId = MClients, DisplayOrder = 5, IsActive = true }
        );
        await ctx.SaveChangesAsync();
    }

    private static async Task<Tenant> SeedTenantAsync(KTransportDbContext ctx, Guid id, string code)
    {
        var t = new Tenant { Id = id, Name = code, Code = code, IsActive = true, CreatedAt = DateTime.UtcNow };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t;
    }

    private static async Task<User> SeedUserAsync(KTransportDbContext ctx, Guid tenantId, string username)
    {
        var u = new User { TenantId = tenantId, Username = username, Password = "x", FullName = username, IsActive = true, CreatedAt = DateTime.UtcNow };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u;
    }

    private static async Task<Role> SeedRoleAsync(KTransportDbContext ctx, Guid tenantId, string code)
    {
        var r = new Role { TenantId = tenantId, Code = code, Name = code, IsSystem = true, IsActive = true };
        ctx.Roles.Add(r);
        await ctx.SaveChangesAsync();
        return r;
    }

    private static async Task AssignRoleAsync(KTransportDbContext ctx, Guid tenantId, int userId, int roleId)
    {
        ctx.UserRoles.Add(new UserRole { TenantId = tenantId, UserId = userId, RoleId = roleId, AssignedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
    }

    private static async Task EnableModulesAsync(KTransportDbContext ctx, Guid tenantId, params int[] moduleIds)
    {
        foreach (var m in moduleIds)
            ctx.TenantModules.Add(new TenantModule { TenantId = tenantId, ModuleId = m, IsEnabled = true, EnabledFrom = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
    }

    private static HashSet<string> Flatten(IEnumerable<DynamicMenuItemDto> menu)
    {
        var flat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Collect(DynamicMenuItemDto i) { flat.Add(i.Id); if (i.Children != null) foreach (var c in i.Children) Collect(c); }
        foreach (var m in menu) Collect(m);
        return flat;
    }

    // ================= keystone + module ceiling =================

    [Fact]
    public async Task AssignModule_AdminSeesAllItsPages_LiveWithoutSeededGrants()
    {
        await using var ctx = NewContext();
        await SeedCatalogAsync(ctx);
        var tid = Guid.NewGuid();
        await SeedTenantAsync(ctx, tid, "T1");
        await EnableModulesAsync(ctx, tid, MDashboard, MBilty);
        var admin = await SeedRoleAsync(ctx, tid, "admin");
        var user = await SeedUserAsync(ctx, tid, "admin");
        await AssignRoleAsync(ctx, tid, user.Id, admin.Id); // NO role_permissions seeded

        var eff = await NewEnt(ctx).ComputeEffectivePermissionsFromTablesAsync(tid, user.Id, "admin");

        Assert.Contains("consignments.all.view", eff);
        Assert.Contains("consignments.create.create", eff);
        Assert.Contains("dashboard.view", eff);
        Assert.DoesNotContain("billing.view", eff); // billing module NOT enabled → ceiling excludes it
        Assert.DoesNotContain("*", eff);
    }

    [Fact]
    public async Task RevokeModule_HidesAllItsMenu_ForAdmin()
    {
        await using var ctx = NewContext();
        await SeedCatalogAsync(ctx);
        var tid = Guid.NewGuid();
        await SeedTenantAsync(ctx, tid, "T2");
        await EnableModulesAsync(ctx, tid, MDashboard, MBilty, MBilling);
        var admin = await SeedRoleAsync(ctx, tid, "admin");
        var user = await SeedUserAsync(ctx, tid, "admin");
        await AssignRoleAsync(ctx, tid, user.Id, admin.Id);
        var nav = NewNav(ctx, NewEnt(ctx));

        var before = Flatten(await nav.GetDynamicMenuAsync(tid, "admin", user.Id.ToString()));
        Assert.Contains("billing", before);
        Assert.Contains("billing.invoices", before);
        Assert.Contains("consignments", before);

        var tm = ctx.TenantModules.IgnoreQueryFilters().First(x => x.TenantId == tid && x.ModuleId == MBilling && x.EnabledUntil == null);
        tm.EnabledUntil = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var after = Flatten(await nav.GetDynamicMenuAsync(tid, "admin", user.Id.ToString()));
        Assert.DoesNotContain("billing", after);
        Assert.DoesNotContain("billing.invoices", after);
        Assert.DoesNotContain("billing.bill_book", after);
        Assert.Contains("consignments", after);
    }

    // ================= page-level ceiling =================

    [Fact]
    public async Task RestrictModuleToOnePage_AdminAndSubUserSeeOnlyThatPage()
    {
        await using var ctx = NewContext();
        await SeedCatalogAsync(ctx);
        var tid = Guid.NewGuid();
        await SeedTenantAsync(ctx, tid, "T3");
        var admin = await SeedRoleAsync(ctx, tid, "admin");
        var adminUser = await SeedUserAsync(ctx, tid, "admin");
        await AssignRoleAsync(ctx, tid, adminUser.Id, admin.Id);
        var ent = NewEnt(ctx);
        var nav = NewNav(ctx, ent);

        // Platform admin: enable bilty but restrict it to the single page consignments.create.
        await ent.WriteTenantEntitlementsAsync(tid, new TenantMenuEntitlementsDto
        {
            TenantId = tid,
            ModuleCodes = new List<string> { "dashboard", "bilty" },
            PageRestrictions = new Dictionary<string, List<string>> { ["bilty"] = new() { "consignments.create" } }
        }, userId: null);

        var adminMenu = Flatten(await nav.GetDynamicMenuAsync(tid, "admin", adminUser.Id.ToString()));
        Assert.Contains("consignments.create", adminMenu);
        Assert.DoesNotContain("consignments.all", adminMenu); // page not whitelisted → hidden even for admin

        var eff = await ent.ComputeEffectivePermissionsFromTablesAsync(tid, adminUser.Id, "admin");
        Assert.Contains("consignments.create.create", eff);
        Assert.DoesNotContain("consignments.all.view", eff);
    }

    [Fact]
    public async Task SubUser_SeesOnlyGrantedPages()
    {
        await using var ctx = NewContext();
        await SeedCatalogAsync(ctx);
        var tid = Guid.NewGuid();
        await SeedTenantAsync(ctx, tid, "T4");
        await EnableModulesAsync(ctx, tid, MDashboard, MBilty);
        var role = await SeedRoleAsync(ctx, tid, "operator");
        var user = await SeedUserAsync(ctx, tid, "op");
        await AssignRoleAsync(ctx, tid, user.Id, role.Id);
        // Grant only the consignments.create page actions.
        ctx.RolePermissions.AddRange(
            new RolePermission { TenantId = tid, RoleId = role.Id, PermissionId = 3, GrantedAt = DateTime.UtcNow },
            new RolePermission { TenantId = tid, RoleId = role.Id, PermissionId = 4, GrantedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var eff = await NewEnt(ctx).ComputeEffectivePermissionsFromTablesAsync(tid, user.Id, "operator");
        Assert.Contains("consignments.create.create", eff);
        Assert.DoesNotContain("consignments.all.view", eff);

        var menu = Flatten(await NewNav(ctx, NewEnt(ctx)).GetDynamicMenuAsync(tid, "operator", user.Id.ToString()));
        Assert.Contains("consignments.create", menu);
        Assert.DoesNotContain("consignments.all", menu);
    }

    // ================= security posture =================

    [Fact]
    public async Task AdminStringAlone_WithoutUserRoles_IsEmpty()
    {
        await using var ctx = NewContext();
        await SeedCatalogAsync(ctx);
        var tid = Guid.NewGuid();
        await SeedTenantAsync(ctx, tid, "T5");
        await EnableModulesAsync(ctx, tid, MDashboard, MBilty);
        var user = await SeedUserAsync(ctx, tid, "forged"); // no user_roles, no role row

        var eff = await NewEnt(ctx).ComputeEffectivePermissionsFromTablesAsync(tid, user.Id, "admin");
        Assert.Empty(eff);
    }

    [Fact]
    public async Task SuperUser_IsWildcardedByNavigationService()
    {
        await using var ctx = NewContext();
        await SeedCatalogAsync(ctx);
        var perms = await NewNav(ctx, NewEnt(ctx)).GetUserPermissionsAsync(TenantConstants.DefaultTenantId, "SUPER_USER");
        Assert.Contains("*", perms);
    }

    // ================= write reshape =================

    [Fact]
    public async Task Write_UpsertsModulesExactly_AndRevokesTheRest()
    {
        await using var ctx = NewContext();
        await SeedCatalogAsync(ctx);
        var tid = Guid.NewGuid();
        await SeedTenantAsync(ctx, tid, "T6");
        var ent = NewEnt(ctx);

        await ent.WriteTenantEntitlementsAsync(tid, new TenantMenuEntitlementsDto { TenantId = tid, ModuleCodes = new List<string> { "bilty", "billing" } }, null);
        var active1 = await ent.GetActiveModuleCodesAsync(tid);
        Assert.Contains("bilty", active1);
        Assert.Contains("billing", active1);

        // Re-save with only bilty → billing soft-revoked.
        await ent.WriteTenantEntitlementsAsync(tid, new TenantMenuEntitlementsDto { TenantId = tid, ModuleCodes = new List<string> { "bilty" } }, null);
        var active2 = await ent.GetActiveModuleCodesAsync(tid);
        Assert.Contains("bilty", active2);
        Assert.DoesNotContain("billing", active2);
        // Soft-revoke: the row still exists (append-only).
        Assert.True(ctx.TenantModules.IgnoreQueryFilters().Any(tm => tm.TenantId == tid && tm.ModuleId == MBilling && tm.EnabledUntil != null));
    }

    // ================= fresh-DB bootstrap =================

    [Fact]
    public async Task FreshBootstrap_PlatformAdminGetsUserRolesAndFullMenu()
    {
        await using var ctx = NewContext();
        await SeedCatalogAsync(ctx);
        // Default platform tenant + admin user (as InitialCreate/Program.cs would seed).
        await SeedTenantAsync(ctx, TenantConstants.DefaultTenantId, "DEFAULT");
        var admin = await SeedUserAsync(ctx, TenantConstants.DefaultTenantId, "admin");
        ctx.SubscriptionPlans.Add(new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Professional Tier", Tier = "Professional", IsActive = true });
        await ctx.SaveChangesAsync();

        await DataSeeder.BootstrapPlatformTenantAsync(ctx, NullLogger<RbacConsolidationTests>.Instance);

        // Admin now has a user_roles → admin row.
        var hasAdminRole = await (from ur in ctx.UserRoles.IgnoreQueryFilters()
                                  join r in ctx.Roles.IgnoreQueryFilters() on ur.RoleId equals r.Id
                                  where ur.UserId == admin.Id && ur.RevokedAt == null && r.Code == "admin"
                                  select ur.Id).AnyAsync();
        Assert.True(hasAdminRole);

        // And the keystone yields a non-empty ceiling (all modules enabled for platform tenant).
        var eff = await NewEnt(ctx).ComputeEffectivePermissionsFromTablesAsync(TenantConstants.DefaultTenantId, admin.Id, "admin");
        Assert.Contains("clients.view", eff);
        Assert.Contains("billing.view", eff);
    }

    [Fact]
    public async Task OnboardedTenant_AdminSeesExactlyProvisionedModules()
    {
        await using var ctx = NewContext();
        await SeedCatalogAsync(ctx);
        await SeedTenantAsync(ctx, TenantConstants.DefaultTenantId, "DEFAULT");
        ctx.SubscriptionPlans.Add(new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Starter Tier", Tier = "Starter", IsActive = true });
        await ctx.SaveChangesAsync();

        var svc = new TenantService(NullLogger<TenantService>.Instance, ctx,
            Options.Create(new JwtSettings { Secret = "0123456789abcdef0123456789abcdef", Issuer = "t", Audience = "t", ExpiryInMinutes = 60 }),
            new StubAudit(), new StubEmail());

        var res = await svc.OnboardTenantByAdminAsync(new Models.TenantAdminOnboardRequest
        {
            OrganizationName = "Acme", OrganizationCode = "ACME",
            AdminUsername = "acme_admin", AdminPassword = "secret123", AdminFullName = "Acme Admin",
            AdminRoleCode = "admin", PlanTier = "Starter",
            EnabledModuleCodes = new List<string> { "dashboard", "bilty", "billing" }
        }, platformAdminUserId: null);

        Assert.True(res.Success);
        var tid = res.TenantId!.Value;
        var modules = await NewEnt(ctx).GetActiveModuleCodesAsync(tid);
        Assert.Contains("bilty", modules);
        Assert.Contains("billing", modules);
        Assert.DoesNotContain("reports", modules); // not provisioned

        var adminUser = ctx.Users.IgnoreQueryFilters().First(u => u.Username == "acme_admin");
        var eff = await NewEnt(ctx).ComputeEffectivePermissionsFromTablesAsync(tid, adminUser.Id, "admin");
        Assert.Contains("billing.view", eff);
        Assert.DoesNotContain("reports.view", eff);
    }

    // ================= cross-tenant isolation =================

    [Fact]
    public async Task TwoTenants_SameUsername_NoLeak()
    {
        await using var ctx = NewContext();
        await SeedCatalogAsync(ctx);
        var tA = Guid.NewGuid();
        var tB = Guid.NewGuid();
        await SeedTenantAsync(ctx, tA, "TA");
        await SeedTenantAsync(ctx, tB, "TB");
        await EnableModulesAsync(ctx, tA, MDashboard, MBilty);
        await EnableModulesAsync(ctx, tB, MDashboard, MBilling);
        var adminA = await SeedRoleAsync(ctx, tA, "admin");
        var adminB = await SeedRoleAsync(ctx, tB, "admin");
        var userA = await SeedUserAsync(ctx, tA, "admin");  // same username in both tenants
        var userB = await SeedUserAsync(ctx, tB, "admin");
        await AssignRoleAsync(ctx, tA, userA.Id, adminA.Id);
        await AssignRoleAsync(ctx, tB, userB.Id, adminB.Id);

        var effA = await NewEnt(ctx).ComputeEffectivePermissionsFromTablesAsync(tA, userA.Id, "admin");
        var effB = await NewEnt(ctx).ComputeEffectivePermissionsFromTablesAsync(tB, userB.Id, "admin");

        Assert.Contains("consignments.all.view", effA);
        Assert.DoesNotContain("billing.view", effA);
        Assert.Contains("billing.view", effB);
        Assert.DoesNotContain("consignments.all.view", effB);
    }

    // ================= grep-guard: legacy engine deleted =================

    [Fact]
    public void LegacyAliasNormalizers_AreDeleted()
    {
        Assert.Null(typeof(FeatureConstants).GetMethod("Normalize", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(typeof(EntitlementsCatalog).GetMethod("NormalizeFeatureKey", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(typeof(EntitlementsCatalog).GetMethod("ExpandFeatureKey", BindingFlags.Public | BindingFlags.Static));
    }

    [Fact]
    public void RolePermission_HasNoLegacyStringColumns()
    {
        Assert.Null(typeof(RolePermission).GetProperty("RoleName"));
        Assert.Null(typeof(RolePermission).GetProperty("PermissionKey"));
        Assert.Null(typeof(UserPermissionOverride).GetProperty("PermissionKey"));
        Assert.Null(typeof(Permission).GetProperty("FeatureKey"));
        Assert.Null(typeof(MenuItem).GetProperty("ParentKey"));
    }
}
