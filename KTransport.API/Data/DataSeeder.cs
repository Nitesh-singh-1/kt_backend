using System;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Services.Rbac;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KTransport.API.Data
{
    /// <summary>
    /// RBAC clean-slate (ADR rbac-menu-consolidation §4/§5): the fresh-DB
    /// bootstrap. After Migrate() + catalog seed, the default platform tenant
    /// gets ALL modules enabled, the 7 system roles (+ grants), and the admin
    /// user's user_roles row — so the platform operator can log in and see
    /// /clients. Then a catalog-consistency validator fails loud on wiring gaps.
    /// Idempotent.
    /// </summary>
    public static class DataSeeder
    {
        // Canonical module catalog (code → name, isPremium, displayOrder). Runtime
        // source of current truth; migrations stay frozen-historical.
        private static readonly (string Code, string Name, bool Premium, int Order)[] CanonicalModules =
        {
            ("dashboard","Dashboard",false,0), ("bilty","Bilty / Consignments",false,1),
            ("pod","Proof of Delivery",false,2), ("trips","Trips & Manifests",false,3),
            ("billing","Billing & Invoicing",false,4), ("master_data","Master Data",false,5),
            ("reports","Reports",false,6), ("vendors","Vendors & Lorry Hire",false,7),
            ("claims","Claims",false,8), ("quotations","Quotations",false,9),
            ("tracking","Live Tracking",true,10), ("analytics","Analytics",true,11),
            ("system","System",false,12), ("trip_settlement","Trip Settlement",false,13),
            ("delivery_settlement","Delivery Settlement",false,14), ("accounting","Accounting & Finance",true,15),
            ("clients","Client Management",false,16),
        };

        /// <summary>Idempotently ensure all 17 canonical modules exist (EF, provider-agnostic).</summary>
        public static async Task EnsureModulesAsync(KTransportDbContext db)
        {
            var existing = (await db.Modules.IgnoreQueryFilters().Select(m => m.Code).ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var (code, name, premium, order) in CanonicalModules)
            {
                if (existing.Contains(code)) continue;
                db.Modules.Add(new Models.Module { Code = code, Name = name, IsPremium = premium, IsActive = true, DisplayOrder = order, CreatedAt = DateTime.UtcNow });
            }
            await db.SaveChangesAsync();
        }

        public static async Task BootstrapPlatformTenantAsync(KTransportDbContext db, ILogger logger)
        {
            var tenantId = TenantConstants.DefaultTenantId;

            // 0. Ensure the full current module catalog is present (runtime truth).
            await EnsureModulesAsync(db);

            // 1. All modules enabled for the platform tenant.
            await TenantProvisioning.SeedTenantModulesAsync(db, tenantId, RbacCatalog.AllModuleCodes, actingUserId: null);

            // 2. 7 system roles + grants (admin gets everything).
            await TenantProvisioning.SeedRolePermissionsAsync(db, tenantId, actingUserId: null);

            // 3. Admin user → admin role.
            var adminUser = await db.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Username == "admin");
            if (adminUser != null)
            {
                await TenantProvisioning.AssignUserRoleAsync(db, tenantId, adminUser.Id, "admin", assignedBy: null);
            }

            logger.LogInformation("Platform-tenant RBAC bootstrap complete (modules + 7 roles + admin user_roles).");

            // 4. Plan default grants (plan_modules) from the canonical plan mapping.
            await SeedPlanModulesAsync(db);
        }

        /// <summary>
        /// Seed plan_modules for Starter / Professional / Enterprise from the
        /// canonical RbacCatalog mapping if not already present. Idempotent.
        /// </summary>
        private static async Task SeedPlanModulesAsync(KTransportDbContext db)
        {
            var moduleIdByCode = await db.Modules.IgnoreQueryFilters()
                .ToDictionaryAsync(m => m.Code, m => m.Id, StringComparer.OrdinalIgnoreCase);
            var plans = await db.SubscriptionPlans.ToListAsync();
            var existing = (await db.PlanModules.Select(pm => new { pm.PlanId, pm.ModuleId }).ToListAsync())
                .Select(x => $"{x.PlanId}|{x.ModuleId}").ToHashSet();

            foreach (var plan in plans)
            {
                foreach (var code in RbacCatalog.ModulesForPlan(plan.Tier))
                {
                    if (!moduleIdByCode.TryGetValue(code, out var mid)) continue;
                    var sig = $"{plan.Id}|{mid}";
                    if (existing.Contains(sig)) continue;
                    db.PlanModules.Add(new Models.PlanModule { PlanId = plan.Id, ModuleId = mid });
                    existing.Add(sig);
                }
            }
            await db.SaveChangesAsync();
        }

        /// <summary>
        /// Relational-only belt-and-suspenders: generically fill any menu_items
        /// with a missing module_id (from the key root against modules) and any
        /// permissions with a missing page_key (from the owning child menu_item),
        /// so a historical drift can never leave the catalog inconsistent at boot.
        /// No-op on a non-relational provider (unit tests).
        /// </summary>
        public static async Task ReconcileCatalogSqlAsync(KTransportDbContext db, ILogger logger)
        {
            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync(@"
UPDATE menu_items mi SET module_id = m.id
FROM modules m
WHERE (mi.module_id IS NULL OR mi.module_id = 0)
  AND m.code = CASE
    WHEN split_part(mi.key, '.', 1) IN ('consignments','gr','bilty') THEN 'bilty'
    WHEN split_part(mi.key, '.', 1) IN ('empty_trips','challan')     THEN 'trips'
    WHEN split_part(mi.key, '.', 1) = 'masterdata'                   THEN 'master_data'
    WHEN split_part(mi.key, '.', 1) = 'saas'                         THEN 'clients'
    ELSE split_part(mi.key, '.', 1)
  END;");

            // A page permission's key is '<page>.<action>'; the owning page is the
            // key with the trailing action stripped. Only set page_key when that
            // resolves to a CHILD menu_item (module-level permissions stay NULL).
            await db.Database.ExecuteSqlRawAsync(@"
UPDATE permissions pr SET page_key = mi.key
FROM menu_items mi
WHERE pr.page_key IS NULL
  AND mi.parent_id IS NOT NULL
  AND lower(mi.key) = lower(regexp_replace(pr.key, '\.[^.]+$', ''));");

            logger.LogInformation("Runtime catalog reconcile (module_id + page_key) complete.");
        }

        /// <summary>
        /// Catalog-consistency validator (fail loud): every active menu_item
        /// resolves to a real module and its module carries ≥1 permission; every
        /// permissions.page_key resolves to a menu_items.key. Throws on a hard
        /// wiring gap so a broken catalog never boots silently.
        /// </summary>
        public static async Task ValidateCatalogAsync(KTransportDbContext db, ILogger logger)
        {
            var moduleIds = (await db.Modules.IgnoreQueryFilters().Select(m => m.Id).ToListAsync()).ToHashSet();
            var menuKeys = (await db.MenuItems.IgnoreQueryFilters().Select(m => m.Key).ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var permModuleIds = (await db.Permissions.IgnoreQueryFilters().Select(p => p.ModuleId).Distinct().ToListAsync()).ToHashSet();

            var errors = new System.Collections.Generic.List<string>();

            var menuItems = await db.MenuItems.IgnoreQueryFilters().Where(m => m.IsActive)
                .Select(m => new { m.Key, m.ModuleId }).ToListAsync();
            foreach (var mi in menuItems)
            {
                if (mi.ModuleId <= 0 || !moduleIds.Contains(mi.ModuleId))
                    errors.Add($"menu_item '{mi.Key}' has no valid module_id ({mi.ModuleId}).");
                else if (!permModuleIds.Contains(mi.ModuleId))
                    logger.LogWarning("Catalog check: menu_item '{Key}' module {Mod} has no permissions.", mi.Key, mi.ModuleId);
            }

            var pageKeys = await db.Permissions.IgnoreQueryFilters()
                .Where(p => p.PageKey != null).Select(p => new { p.Key, p.PageKey }).ToListAsync();
            foreach (var p in pageKeys)
            {
                if (!menuKeys.Contains(p.PageKey!))
                    errors.Add($"permission '{p.Key}' has page_key '{p.PageKey}' that resolves to no menu_item.");
            }

            if (errors.Count > 0)
            {
                var msg = "Catalog consistency validation FAILED:\n" + string.Join("\n", errors);
                logger.LogError(msg);
                throw new InvalidOperationException(msg);
            }
            logger.LogInformation("Catalog consistency validation passed ({MenuItems} menu items).", menuItems.Count);
        }
    }
}
