using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services.Rbac
{
    /// <summary>
    /// RBAC clean-slate: the ONE provisioning/seed code path shared by
    /// self-signup onboarding, platform-admin onboarding and the fresh-DB
    /// bootstrap seeder. plan → tenant_modules (+ optional pages) → 7 system
    /// roles → role_permissions (ceiling-bounded, admin unconditional) → admin
    /// user_roles row. Everything is idempotent.
    /// </summary>
    public static class TenantProvisioning
    {
        private static readonly (string Code, string Name, string Description)[] SystemRoleTemplates =
        {
            ("admin",              "Administrator",       "Full tenant management + all enabled modules"),
            ("operations_manager", "Operations Manager",  "Operational modules + reports"),
            ("supervisor",         "Supervisor",          "Approve / edit operational docs"),
            ("operator",           "Operator",            "Create own documents only"),
            ("billing_executive",  "Billing Executive",   "Billing + invoices CRUD"),
            ("accountant",         "Accountant",          "Financial reports + view-only operations"),
            ("viewer",             "Viewer",              "View-only everything the tenant has"),
        };

        /// <summary>Ensure the 7 system roles exist for the tenant. Returns code→id.</summary>
        public static async Task<Dictionary<string, int>> EnsureSystemRolesAsync(KTransportDbContext db, Guid tenantId, int? actingUserId)
        {
            var existing = await db.Roles.IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId)
                .ToListAsync();
            var byCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in existing) byCode[r.Code] = r.Id;

            foreach (var (code, name, desc) in SystemRoleTemplates)
            {
                if (byCode.ContainsKey(code)) continue;
                var role = new Role
                {
                    TenantId = tenantId,
                    Code = code,
                    Name = name,
                    Description = desc,
                    IsSystem = true,
                    IsActive = true,
                    CreatedBy = actingUserId,
                    CreatedAt = DateTime.UtcNow
                };
                db.Roles.Add(role);
            }
            await db.SaveChangesAsync();

            byCode.Clear();
            foreach (var r in await db.Roles.IgnoreQueryFilters().Where(r => r.TenantId == tenantId).ToListAsync())
                byCode[r.Code] = r.Id;
            return byCode;
        }

        /// <summary>Upsert tenant_modules to include the given module codes (idempotent, additive).</summary>
        public static async Task SeedTenantModulesAsync(KTransportDbContext db, Guid tenantId, IEnumerable<string> moduleCodes, int? actingUserId)
        {
            var codes = moduleCodes?.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                        ?? new List<string>();
            if (codes.Count == 0) return;

            var moduleByCode = (await db.Modules.IgnoreQueryFilters().ToListAsync())
                .ToDictionary(m => m.Code, m => m.Id, StringComparer.OrdinalIgnoreCase);

            var activeIds = await db.TenantModules.IgnoreQueryFilters()
                .Where(tm => tm.TenantId == tenantId && tm.EnabledUntil == null)
                .Select(tm => tm.ModuleId).ToListAsync();
            var activeSet = new HashSet<int>(activeIds);

            foreach (var code in codes)
            {
                if (!moduleByCode.TryGetValue(code, out var modId)) continue;
                if (activeSet.Contains(modId)) continue;
                db.TenantModules.Add(new TenantModule
                {
                    TenantId = tenantId,
                    ModuleId = modId,
                    IsEnabled = true,
                    EnabledFrom = DateTime.UtcNow,
                    EnabledBy = actingUserId,
                    CreatedAt = DateTime.UtcNow
                });
                activeSet.Add(modId);
            }
            await db.SaveChangesAsync();
        }

        /// <summary>
        /// Seed role_permissions for the 7 system roles via SystemRolePermissionMatrix.
        /// admin gets EVERY catalog permission; others are bounded to the tenant's
        /// enabled modules. Additive/idempotent (no revokes).
        /// </summary>
        public static async Task SeedRolePermissionsAsync(KTransportDbContext db, Guid tenantId, int? actingUserId)
        {
            var roleIdByCode = await EnsureSystemRolesAsync(db, tenantId, actingUserId);

            var catalog = await db.Permissions.IgnoreQueryFilters().ToListAsync();

            var enabledModuleIds = new HashSet<int>(await db.TenantModules.IgnoreQueryFilters()
                .Where(tm => tm.TenantId == tenantId && tm.EnabledUntil == null)
                .Select(tm => tm.ModuleId).ToListAsync());

            var alreadyGranted = (await db.RolePermissions.IgnoreQueryFilters()
                .Where(rp => rp.TenantId == tenantId && rp.RevokedAt == null)
                .Select(rp => new { rp.RoleId, rp.PermissionId })
                .ToListAsync())
                .Select(x => $"{x.RoleId}|{x.PermissionId}")
                .ToHashSet();

            foreach (var perm in catalog)
            {
                foreach (var code in RbacCatalog.SystemRoleCodes)
                {
                    bool isAdmin = string.Equals(code, "admin", StringComparison.OrdinalIgnoreCase);
                    if (!isAdmin && !enabledModuleIds.Contains(perm.ModuleId)) continue;
                    if (!SystemRolePermissionMatrix.Grants(code, perm)) continue;
                    if (!roleIdByCode.TryGetValue(code, out var roleId)) continue;

                    var sig = $"{roleId}|{perm.Id}";
                    if (alreadyGranted.Contains(sig)) continue;
                    db.RolePermissions.Add(new RolePermission
                    {
                        TenantId = tenantId,
                        RoleId = roleId,
                        PermissionId = perm.Id,
                        GrantedAt = DateTime.UtcNow,
                        GrantedBy = actingUserId
                    });
                    alreadyGranted.Add(sig);
                }
            }
            await db.SaveChangesAsync();
        }

        /// <summary>Assign a user to a role (creating a custom role if the code is unknown). Idempotent.</summary>
        public static async Task AssignUserRoleAsync(KTransportDbContext db, Guid tenantId, int userId, string roleCode, int? assignedBy)
        {
            if (string.IsNullOrWhiteSpace(roleCode)) roleCode = "admin";
            var role = await db.Roles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Code.ToLower() == roleCode.ToLower());
            if (role == null)
            {
                role = new Role
                {
                    TenantId = tenantId,
                    Code = roleCode.Trim().ToLowerInvariant(),
                    Name = roleCode.Trim(),
                    Description = "Custom role",
                    IsSystem = false,
                    IsActive = true,
                    CreatedBy = assignedBy,
                    CreatedAt = DateTime.UtcNow
                };
                db.Roles.Add(role);
                await db.SaveChangesAsync();
            }

            var exists = await db.UserRoles.IgnoreQueryFilters()
                .AnyAsync(ur => ur.TenantId == tenantId && ur.UserId == userId && ur.RoleId == role.Id && ur.RevokedAt == null);
            if (!exists)
            {
                db.UserRoles.Add(new UserRole
                {
                    TenantId = tenantId,
                    UserId = userId,
                    RoleId = role.Id,
                    AssignedAt = DateTime.UtcNow,
                    AssignedBy = assignedBy
                });
                await db.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Full tenant provisioning used by both onboarding paths: tenant_modules
        /// (from explicit codes, else the plan default), 7 roles + grants, and the
        /// admin user's user_roles row.
        /// </summary>
        public static async Task ProvisionTenantAsync(
            KTransportDbContext db, Guid tenantId, int adminUserId, string adminRoleCode,
            string planTier, IReadOnlyList<string>? explicitModuleCodes, int? actingUserId)
        {
            var moduleCodes = (explicitModuleCodes != null && explicitModuleCodes.Count > 0)
                ? explicitModuleCodes.ToList()
                : RbacCatalog.ModulesForPlan(planTier).ToList();
            if (!moduleCodes.Contains("dashboard", StringComparer.OrdinalIgnoreCase)) moduleCodes.Add("dashboard");
            if (!moduleCodes.Contains("system", StringComparer.OrdinalIgnoreCase)) moduleCodes.Add("system");

            await SeedTenantModulesAsync(db, tenantId, moduleCodes, actingUserId);
            await SeedRolePermissionsAsync(db, tenantId, actingUserId);
            await AssignUserRoleAsync(db, tenantId, adminUserId, adminRoleCode, actingUserId);
        }
    }
}
