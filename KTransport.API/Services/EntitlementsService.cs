using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KTransport.API.Services
{
    public interface IEntitlementsService
    {
        /// <summary>
        /// The ONE effective-permission function (ADR rbac-menu-consolidation §2.2).
        /// Pure structural joins — no alias normalization, no enabled_feature_keys,
        /// no role_name/permission_key string matching. SUPER_USER wildcard is
        /// applied by NavigationService, not here.
        /// </summary>
        Task<HashSet<string>> ComputeEffectivePermissionsFromTablesAsync(Guid tenantId, int? userId, string? role);

        /// <summary>The tenant's active module codes (for [RequireFeature] resolution).</summary>
        Task<HashSet<string>> GetActiveModuleCodesAsync(Guid tenantId);

        /// <summary>
        /// Platform-admin entitlement write (contract §B): upsert tenant_modules to
        /// exactly moduleCodes, upsert tenant_module_pages per pageRestrictions, and
        /// upsert report entitlements. No admin-grant reseed (the keystone computes
        /// admin live), no enabled_feature_keys mirror, no dotted override expansion.
        /// </summary>
        Task WriteTenantEntitlementsAsync(Guid tenantId, TenantMenuEntitlementsDto newDto, int? userId);
    }

    /// <summary>
    /// RBAC clean-slate single engine. permissions ⨝ tenant_modules
    /// (+ tenant_module_pages page ceiling) ⨝ role_permissions ⨝ user_roles,
    /// plus user_permission_overrides. The genuine-admin keystone grants the
    /// whole ceiling live. No second engine, no legacy columns.
    /// </summary>
    public class EntitlementsService : IEntitlementsService
    {
        private readonly KTransportDbContext _db;
        private readonly ILogger<EntitlementsService> _log;

        public EntitlementsService(KTransportDbContext db, ILogger<EntitlementsService> log)
        {
            _db = db;
            _log = log;
        }

        public async Task<HashSet<string>> GetActiveModuleCodesAsync(Guid tenantId)
        {
            var codes = await (
                from tm in _db.TenantModules.IgnoreQueryFilters()
                join m in _db.Modules.IgnoreQueryFilters() on tm.ModuleId equals m.Id
                where tm.TenantId == tenantId && tm.EnabledUntil == null
                select m.Code).ToListAsync();
            return new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<HashSet<string>> ComputeEffectivePermissionsFromTablesAsync(Guid tenantId, int? userId, string? role)
        {
            var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // --- active module ceiling ---
            var activeModuleIds = new HashSet<int>(await _db.TenantModules
                .IgnoreQueryFilters()
                .Where(tm => tm.TenantId == tenantId && tm.EnabledUntil == null)
                .Select(tm => tm.ModuleId)
                .ToListAsync());

            if (activeModuleIds.Count == 0) return effective;

            // --- active page whitelist (tenant_module_pages) ---
            var activePageRows = await _db.TenantModulePages
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId && p.EnabledUntil == null)
                .Select(p => new { p.ModuleId, p.PageKey })
                .ToListAsync();
            var pagesByModule = activePageRows
                .GroupBy(p => p.ModuleId)
                .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.PageKey), StringComparer.OrdinalIgnoreCase));

            // --- ceiling(tenant): permissions gated by module + page ---
            var allPermissions = await _db.Permissions.IgnoreQueryFilters()
                .Select(p => new { p.Id, p.Key, p.ModuleId, p.PageKey })
                .ToListAsync();

            var ceilingKeysById = new Dictionary<int, string>();
            foreach (var p in allPermissions)
            {
                if (string.IsNullOrWhiteSpace(p.Key)) continue;
                if (!activeModuleIds.Contains(p.ModuleId)) continue;
                if (!string.IsNullOrEmpty(p.PageKey)
                    && pagesByModule.TryGetValue(p.ModuleId, out var whitelist)
                    && !whitelist.Contains(p.PageKey))
                {
                    continue; // module is page-restricted and this page is not whitelisted
                }
                ceilingKeysById[p.Id] = p.Key;
            }
            var ceilingKeys = new HashSet<string>(ceilingKeysById.Values, StringComparer.OrdinalIgnoreCase);

            // --- genuine-admin keystone ---
            var activeRoleIds = new List<int>();
            if (userId.HasValue)
            {
                activeRoleIds = await _db.UserRoles
                    .IgnoreQueryFilters()
                    .Where(ur => ur.TenantId == tenantId && ur.UserId == userId.Value && ur.RevokedAt == null)
                    .Select(ur => ur.RoleId)
                    .ToListAsync();
            }

            bool isGenuineAdmin = false;
            if (activeRoleIds.Count > 0)
            {
                isGenuineAdmin = await _db.Roles
                    .IgnoreQueryFilters()
                    .AnyAsync(r => r.TenantId == tenantId
                                && activeRoleIds.Contains(r.Id)
                                && r.IsActive
                                && RbacRoles.AdminRoleCodeList.Contains(r.Code.ToLower()));
            }

            if (isGenuineAdmin)
            {
                // Admin → the entire ceiling (every entitled page of every enabled module).
                foreach (var k in ceilingKeys) effective.Add(k);
                return effective;
            }

            // --- normal user: (role grants ∪ user grants ∖ user revokes) ∩ ceiling ---
            if (activeRoleIds.Count > 0)
            {
                var rolePermIds = await _db.RolePermissions
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId && r.RevokedAt == null && activeRoleIds.Contains(r.RoleId))
                    .Select(r => r.PermissionId)
                    .ToListAsync();
                foreach (var pid in rolePermIds)
                    if (ceilingKeysById.TryGetValue(pid, out var key)) effective.Add(key);
            }

            if (userId.HasValue)
            {
                var overrides = await _db.UserPermissionOverrides
                    .IgnoreQueryFilters()
                    .Where(o => o.TenantId == tenantId && o.UserId == userId.Value && o.SupersededBy == null)
                    .Select(o => new { o.PermissionId, o.IsGranted })
                    .ToListAsync();
                foreach (var o in overrides)
                {
                    if (!ceilingKeysById.TryGetValue(o.PermissionId, out var key)) continue;
                    if (o.IsGranted) effective.Add(key);
                    else effective.Remove(key);
                }
            }

            return effective;
        }

        public async Task WriteTenantEntitlementsAsync(Guid tenantId, TenantMenuEntitlementsDto newDto, int? userId)
        {
            var allModules = await _db.Modules.IgnoreQueryFilters().ToListAsync();
            var moduleByCode = allModules.ToDictionary(m => m.Code, m => m, StringComparer.OrdinalIgnoreCase);

            // ---- 1. resolve requested module set ----
            var requestedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in newDto.ModuleCodes ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(code)) continue;
                if (!moduleByCode.ContainsKey(code))
                {
                    _log.LogWarning("WriteTenantEntitlementsAsync tenant={TenantId}: unknown module code '{Code}', skipping.", tenantId, code);
                    continue;
                }
                requestedCodes.Add(code);
            }
            var requestedModuleIds = new HashSet<int>(requestedCodes.Select(c => moduleByCode[c].Id));

            // ---- 2. upsert tenant_modules to exactly requestedModuleIds ----
            var existingTenantModules = await _db.TenantModules
                .IgnoreQueryFilters()
                .Where(tm => tm.TenantId == tenantId)
                .ToListAsync();

            foreach (var modId in requestedModuleIds)
            {
                var activeRow = existingTenantModules.FirstOrDefault(tm => tm.ModuleId == modId && tm.EnabledUntil == null);
                if (activeRow == null)
                {
                    _db.TenantModules.Add(new TenantModule
                    {
                        TenantId = tenantId,
                        ModuleId = modId,
                        IsEnabled = true,
                        EnabledFrom = DateTime.UtcNow,
                        EnabledUntil = null,
                        EnabledBy = userId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else if (!activeRow.IsEnabled)
                {
                    activeRow.IsEnabled = true;
                }
            }
            foreach (var tm in existingTenantModules
                .Where(tm => tm.EnabledUntil == null && !requestedModuleIds.Contains(tm.ModuleId)))
            {
                tm.EnabledUntil = DateTime.UtcNow;
            }

            // ---- 3. upsert tenant_module_pages per pageRestrictions ----
            var existingPages = await _db.TenantModulePages
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId)
                .ToListAsync();

            // Normalize the requested page restrictions to module ids.
            var pageWhitelistByModuleId = new Dictionary<int, HashSet<string>>();
            foreach (var kv in newDto.PageRestrictions ?? new Dictionary<string, List<string>>())
            {
                if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                if (!moduleByCode.TryGetValue(kv.Key, out var mod)) continue;
                var pages = (kv.Value ?? new List<string>())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                // An empty whitelist means "no restriction" (all pages) — skip it.
                if (pages.Count > 0) pageWhitelistByModuleId[mod.Id] = pages;
            }

            // For each active module: if it has a whitelist, reconcile; else revoke all its page rows.
            foreach (var modId in requestedModuleIds)
            {
                var activePageRows = existingPages.Where(p => p.ModuleId == modId && p.EnabledUntil == null).ToList();
                if (pageWhitelistByModuleId.TryGetValue(modId, out var whitelist))
                {
                    foreach (var row in activePageRows.Where(r => !whitelist.Contains(r.PageKey)))
                        row.EnabledUntil = DateTime.UtcNow;
                    var activeKeys = activePageRows.Where(r => r.EnabledUntil == null)
                        .Select(r => r.PageKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var pk in whitelist.Where(k => !activeKeys.Contains(k)))
                    {
                        _db.TenantModulePages.Add(new TenantModulePage
                        {
                            TenantId = tenantId,
                            ModuleId = modId,
                            PageKey = pk,
                            EnabledFrom = DateTime.UtcNow,
                            EnabledBy = userId
                        });
                    }
                }
                else
                {
                    // No restriction for this module → all pages entitled → revoke any page rows.
                    foreach (var row in activePageRows) row.EnabledUntil = DateTime.UtcNow;
                }
            }
            // Revoked modules: revoke their page rows too (harmless; module gate already hides them).
            foreach (var row in existingPages.Where(p => p.EnabledUntil == null && !requestedModuleIds.Contains(p.ModuleId)))
                row.EnabledUntil = DateTime.UtcNow;

            // ---- 4. report entitlements ----
            if (newDto.Reports != null && newDto.Reports.Count > 0)
            {
                var existingReports = await _db.TenantReportEntitlements
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId)
                    .ToListAsync();
                var existingByKey = existingReports.ToDictionary(r => r.ReportKey, r => r, StringComparer.OrdinalIgnoreCase);

                foreach (var rep in newDto.Reports)
                {
                    if (string.IsNullOrWhiteSpace(rep.ReportKey)) continue;
                    if (existingByKey.TryGetValue(rep.ReportKey, out var row))
                    {
                        if (row.IsEnabled != rep.IsEnabled)
                        {
                            row.IsEnabled = rep.IsEnabled;
                            row.UpdatedAt = DateTime.UtcNow;
                            row.UpdatedBy = userId;
                        }
                    }
                    else
                    {
                        _db.TenantReportEntitlements.Add(new TenantReportEntitlement
                        {
                            TenantId = tenantId,
                            ReportKey = rep.ReportKey,
                            IsEnabled = rep.IsEnabled,
                            UpdatedAt = DateTime.UtcNow,
                            UpdatedBy = userId
                        });
                    }
                }
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _log.LogWarning(ex, "WriteTenantEntitlementsAsync tenant={TenantId}: unique-violation; treating as idempotent re-run", tenantId);
            }
        }
    }
}
