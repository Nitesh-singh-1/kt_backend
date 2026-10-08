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
        Task<HashSet<string>> ComputeEffectivePermissionsFromTablesAsync(Guid tenantId, int? userId, string? role);

        [Obsolete("Phase 3 removed the JSON source. These methods now log a warning and return an empty summary. Will be deleted in next release.")]
        Task<BackfillSummaryDto> BackfillFromJsonAsync();

        [Obsolete("Phase 3 removed the JSON source. These methods now log a warning and return an empty summary. Will be deleted in next release.")]
        Task<BackfillSummaryDto> BackfillTenantAsync(Guid tenantId, TenantMenuEntitlementsDto json);

        [Obsolete("Phase 3 removed the JSON source. These methods now log a warning and return an empty summary. Will be deleted in next release.")]
        Task<BackfillSummaryDto> BackfillTenantAsync(Guid tenantId, TenantMenuEntitlementsDto json, int? actingUserId);

        /// <summary>
        /// TASK-044 Phase 3: writes the entitlements DTO into the normalized tables.
        /// Renamed from DualWriteFromJsonUpdateAsync — no JSON column is written to.
        /// </summary>
        Task WriteTenantEntitlementsAsync(Guid tenantId, TenantMenuEntitlementsDto newDto, int? userId);
    }

    /// <summary>
    /// Writes/reads the normalized entitlement tables. In Phase 3 these tables are
    /// the sole source of truth — the legacy JSON column has been dropped.
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

        public async Task<HashSet<string>> ComputeEffectivePermissionsFromTablesAsync(Guid tenantId, int? userId, string? role)
        {
            var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var subscription = await _db.TenantEntitlementSubscriptions
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId && s.EffectiveUntil == null)
                .OrderByDescending(s => s.EffectiveFrom)
                .FirstOrDefaultAsync();

            var rawFeatureKeys = subscription?.EnabledFeatureKeys ?? new List<string>();
            var tenantFeatureKeys = new HashSet<string>(
                rawFeatureKeys.Select(EntitlementsCatalog.NormalizeFeatureKey),
                StringComparer.OrdinalIgnoreCase);

            foreach (var k in rawFeatureKeys) tenantFeatureKeys.Add(k);

            // Authoritative module set from tenant_modules
            var tenantModuleCodes = await _db.TenantModules
                .IgnoreQueryFilters()
                .Where(tm => tm.TenantId == tenantId
                             && tm.IsEnabled
                             && tm.EnabledUntil == null)
                .Join(_db.Modules.IgnoreQueryFilters(), tm => tm.ModuleId, m => m.Id, (tm, m) => m.Code)
                .ToListAsync();
            var moduleCodeSet = new HashSet<string>(tenantModuleCodes, StringComparer.OrdinalIgnoreCase);

            // If tenant_modules is empty, derive from tenantFeatureKeys
            if (moduleCodeSet.Count == 0 && tenantFeatureKeys.Count > 0)
            {
                foreach (var k in tenantFeatureKeys)
                {
                    var m = ResolveModuleCode(k);
                    if (m != null) moduleCodeSet.Add(m);
                }
            }

            List<string> userGrantedPermKeys = new();
            List<string> userRevokedPermKeys = new();
            if (userId.HasValue)
            {
                var overrides = await _db.UserPermissionOverrides
                    .IgnoreQueryFilters()
                    .Where(o => o.TenantId == tenantId && o.UserId == userId.Value && o.SupersededBy == null)
                    .Select(o => new { o.PermissionKey, o.IsGranted })
                    .ToListAsync();
                userGrantedPermKeys = overrides.Where(o => o.IsGranted).Select(o => o.PermissionKey).ToList();
                userRevokedPermKeys = overrides.Where(o => !o.IsGranted).Select(o => o.PermissionKey).ToList();
            }

            // TASK-046 Phase 1: resolve role grants via user_roles M2M when a
            // userId is supplied. Fall back to the legacy role string when the
            // user has no active user_roles row (should not happen post-backfill).
            List<string> rolePermKeys = new();
            if (userId.HasValue)
            {
                var activeRoleIds = await _db.UserRoles
                    .IgnoreQueryFilters()
                    .Where(ur => ur.TenantId == tenantId && ur.UserId == userId.Value && ur.RevokedAt == null)
                    .Select(ur => ur.RoleId)
                    .ToListAsync();

                if (activeRoleIds.Count > 0)
                {
                    rolePermKeys = await _db.RolePermissions
                        .IgnoreQueryFilters()
                        .Where(r => r.TenantId == tenantId && r.RevokedAt == null && r.RoleId != null && activeRoleIds.Contains(r.RoleId.Value))
                        .Select(r => r.PermissionKey)
                        .ToListAsync();
                }
            }

            if (rolePermKeys.Count == 0 && !string.IsNullOrWhiteSpace(role))
            {
                // Fallback: legacy role_name string match.
                rolePermKeys = await _db.RolePermissions
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId && r.RoleName.ToLower() == role.ToLower() && r.RevokedAt == null)
                    .Select(r => r.PermissionKey)
                    .ToListAsync();
            }

            // ADR authorization-rbac-architecture.md §F:
            //   effective = (role grants ∪ user grants ∖ user revokes) ∩ tenant_modules
            var sourceKeys = new HashSet<string>(rolePermKeys, StringComparer.OrdinalIgnoreCase);
            foreach (var k in userGrantedPermKeys) sourceKeys.Add(k);
            foreach (var r in userRevokedPermKeys) sourceKeys.Remove(r);

            foreach (var permKey in sourceKeys)
            {
                if (string.IsNullOrWhiteSpace(permKey)) continue;
                var baseFeature = ExtractBaseFeatureKey(permKey);
                var normalizedBase = EntitlementsCatalog.NormalizeFeatureKey(baseFeature);
                var rootFeature = baseFeature.Contains('.') ? baseFeature.Substring(0, baseFeature.IndexOf('.')) : baseFeature;
                var normalizedRoot = EntitlementsCatalog.NormalizeFeatureKey(rootFeature);

                bool isParentFeatureSubscribed =
                    (baseFeature.Equals("delivery_settlement", StringComparison.OrdinalIgnoreCase) &&
                     (tenantFeatureKeys.Contains("consignments") || tenantFeatureKeys.Contains("bilty") || tenantFeatureKeys.Contains("gr"))) ||
                    ((baseFeature.Equals("trip_settlement", StringComparison.OrdinalIgnoreCase) || baseFeature.Equals("empty_trips", StringComparison.OrdinalIgnoreCase)) &&
                     (tenantFeatureKeys.Contains("trips") || tenantFeatureKeys.Contains("challan") || tenantFeatureKeys.Contains("trip")));

                bool hasGranularChildrenForRoot = tenantFeatureKeys.Any(k =>
                    k.StartsWith(rootFeature + ".", StringComparison.OrdinalIgnoreCase) ||
                    k.StartsWith(normalizedRoot + ".", StringComparison.OrdinalIgnoreCase));

                bool isFeatureSubscribed =
                    tenantFeatureKeys.Contains(baseFeature) ||
                    tenantFeatureKeys.Contains(normalizedBase) ||
                    (!hasGranularChildrenForRoot && (tenantFeatureKeys.Contains(rootFeature) || tenantFeatureKeys.Contains(normalizedRoot))) ||
                    isParentFeatureSubscribed;

                // Legacy intersection — stays as rollback safety.
                if (tenantFeatureKeys.Count > 0 && !isFeatureSubscribed)
                {
                    continue;
                }

                if (moduleCodeSet.Count > 0)
                {
                    var moduleCode = ResolveModuleCode(baseFeature);
                    if (moduleCode != null && !moduleCodeSet.Contains(moduleCode))
                    {
                        bool isParentModuleEnabled =
                            (moduleCode.Equals("delivery_settlement", StringComparison.OrdinalIgnoreCase) && moduleCodeSet.Contains("bilty")) ||
                            (moduleCode.Equals("trip_settlement", StringComparison.OrdinalIgnoreCase) && moduleCodeSet.Contains("trips"));
                        if (!isParentModuleEnabled)
                        {
                            continue;
                        }
                    }
                }

                effective.Add(permKey);
            }

            return effective;
        }

        /// <summary>
        /// TASK-046 Phase 1 shim: map a feature-key (e.g. "billing.bill_book")
        /// to its module code (e.g. "billing"). Returns null for feature keys
        /// outside the known module space (which then skip the module gate).
        /// Phase 2 replaces this with a direct permissions.module_id FK.
        /// </summary>
        public static string? ResolveModuleCode(string featureKey)
        {
            if (string.IsNullOrWhiteSpace(featureKey)) return null;
            var fk = featureKey.ToLowerInvariant();
            if (fk.StartsWith("consignments") || fk.StartsWith("gr") || fk == "bilty") return "bilty";
            if (fk == "delivery_settlement") return "delivery_settlement";
            if (fk == "trip_settlement") return "trip_settlement";
            if (fk.StartsWith("empty_trips") || fk.StartsWith("trips") || fk.StartsWith("challan")) return "trips";
            if (fk.StartsWith("billing") || fk == "bill_book") return "billing";
            if (fk.StartsWith("pod")) return "pod";
            if (fk.StartsWith("reports")) return "reports";
            if (fk.StartsWith("master_data")) return "master_data";
            if (fk.StartsWith("quotations")) return "quotations";
            if (fk.StartsWith("vendors")) return "vendors";
            if (fk.StartsWith("claims")) return "claims";
            if (fk.StartsWith("tracking")) return "tracking";
            if (fk.StartsWith("analytics")) return "analytics";
            if (fk.StartsWith("dashboard")) return "dashboard";
            if (fk.StartsWith("system") || fk == "clients") return "system";
            return null;
        }

        private static string ExtractBaseFeatureKey(string permissionKey)
        {
            if (string.IsNullOrWhiteSpace(permissionKey)) return string.Empty;
            var idx = permissionKey.LastIndexOf('.');
            if (idx <= 0) return permissionKey;
            var tail = permissionKey.Substring(idx + 1).ToLowerInvariant();
            if (tail is "view" or "create" or "edit" or "delete" or "print" or "approve" or "export")
            {
                return permissionKey.Substring(0, idx);
            }
            return permissionKey;
        }

        [Obsolete("Phase 3 removed the JSON source. These methods now log a warning and return an empty summary. Will be deleted in next release.")]
        public Task<BackfillSummaryDto> BackfillFromJsonAsync()
        {
            _log.LogWarning("BackfillFromJsonAsync invoked, but the menu_entitlements_json source was dropped in Phase 3. Returning an empty summary. Use PUT /api/configuration/menu-entitlements for new grants.");
            return Task.FromResult(new BackfillSummaryDto { TenantsProcessed = 0 });
        }

        [Obsolete("Phase 3 removed the JSON source. These methods now log a warning and return an empty summary. Will be deleted in next release.")]
        public Task<BackfillSummaryDto> BackfillTenantAsync(Guid tenantId, TenantMenuEntitlementsDto json)
            => BackfillTenantAsync(tenantId, json, actingUserId: null);

        [Obsolete("Phase 3 removed the JSON source. These methods now log a warning and return an empty summary. Will be deleted in next release.")]
        public Task<BackfillSummaryDto> BackfillTenantAsync(Guid tenantId, TenantMenuEntitlementsDto json, int? actingUserId)
        {
            _log.LogWarning("BackfillTenantAsync tenant={TenantId} invoked, but Phase 3 removed the JSON source. Returning an empty summary.", tenantId);
            return Task.FromResult(new BackfillSummaryDto { TenantsProcessed = 0 });
        }

        /// <summary>
        /// Writes the DTO's entitlements into the normalized tables. Atomically updates
        /// TenantEntitlementSubscriptions, TenantModules, TenantReportEntitlements,
        /// and RolePermissions.
        /// </summary>
        public async Task WriteTenantEntitlementsAsync(Guid tenantId, TenantMenuEntitlementsDto newDto, int? userId)
        {
            // ---- 1. Canonical Feature Keys & Module Codes ----
            var rawKeys = newDto.EnabledMenuKeys ?? new List<string>();
            var canonicalKeys = rawKeys
                .Select(EntitlementsCatalog.NormalizeFeatureKey)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!canonicalKeys.Contains("dashboard")) canonicalKeys.Insert(0, "dashboard");
            if (!canonicalKeys.Contains("system")) canonicalKeys.Add("system");
            if (!canonicalKeys.Contains("system.settings")) canonicalKeys.Add("system.settings");

            var allFeatureKeysToPersist = canonicalKeys.Union(rawKeys, StringComparer.OrdinalIgnoreCase).ToList();

            // ---- 2. TenantEntitlementSubscription ----
            var activeSub = await _db.TenantEntitlementSubscriptions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.EffectiveUntil == null);

            if (activeSub == null)
            {
                _db.TenantEntitlementSubscriptions.Add(new TenantEntitlementSubscription
                {
                    TenantId = tenantId,
                    PlanTier = string.IsNullOrWhiteSpace(newDto.PlanTier) ? "Enterprise" : newDto.PlanTier,
                    EnabledFeatureKeys = allFeatureKeysToPersist,
                    EffectiveFrom = DateTime.UtcNow,
                    EffectiveUntil = null,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                });
            }
            else
            {
                activeSub.EnabledFeatureKeys = allFeatureKeysToPersist;
                activeSub.PlanTier = string.IsNullOrWhiteSpace(newDto.PlanTier) ? activeSub.PlanTier : newDto.PlanTier!;
            }

            // ---- 3. Sync TenantModules Table ----
            var moduleCodes = canonicalKeys
                .Select(ResolveModuleCode)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (moduleCodes.Contains("bilty") && !moduleCodes.Contains("delivery_settlement"))
            {
                moduleCodes.Add("delivery_settlement");
            }
            if (moduleCodes.Contains("trips") && !moduleCodes.Contains("trip_settlement"))
            {
                moduleCodes.Add("trip_settlement");
            }

            var allModules = await _db.Modules.IgnoreQueryFilters().ToListAsync();
            var moduleByCode = allModules.ToDictionary(m => m.Code.ToLowerInvariant(), m => m.Id);

            var existingTenantModules = await _db.TenantModules
                .IgnoreQueryFilters()
                .Where(tm => tm.TenantId == tenantId)
                .ToListAsync();

            var activeModuleIds = new HashSet<int>();
            foreach (var code in moduleCodes)
            {
                if (moduleByCode.TryGetValue(code.ToLowerInvariant(), out var modId))
                {
                    activeModuleIds.Add(modId);
                    var existingActive = existingTenantModules.FirstOrDefault(tm => tm.ModuleId == modId && tm.EnabledUntil == null);
                    if (existingActive == null)
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
                    else if (!existingActive.IsEnabled)
                    {
                        existingActive.IsEnabled = true;
                    }
                }
            }

            foreach (var tm in existingTenantModules.Where(tm => tm.EnabledUntil == null && !activeModuleIds.Contains(tm.ModuleId)))
            {
                tm.EnabledUntil = DateTime.UtcNow;
            }

            // ---- 4. TenantReportEntitlements ----
            if (newDto.Reports != null)
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

            // ---- 5. Ensure Admin Role and Seed RolePermissions for Admin ----
            var adminRole = await _db.Roles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Code.ToLower() == "admin");

            if (adminRole == null)
            {
                adminRole = new Role
                {
                    TenantId = tenantId,
                    Code = "admin",
                    Name = "Administrator",
                    Description = "Full access to all subscribed organization modules",
                    IsSystem = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };
                _db.Roles.Add(adminRole);
                await _db.SaveChangesAsync();
            }

            // Ensure all tenant admin users have a user_roles row
            var adminUsers = await _db.Users
                .IgnoreQueryFilters()
                .Where(u => u.TenantId == tenantId && (u.Role == "admin" || u.Role == "tenantadmin" || u.Role == "superadmin"))
                .ToListAsync();

            var existingUserRoles = await _db.UserRoles
                .IgnoreQueryFilters()
                .Where(ur => ur.TenantId == tenantId && ur.RoleId == adminRole.Id && ur.RevokedAt == null)
                .Select(ur => ur.UserId)
                .ToListAsync();
            var existingUserRoleSet = new HashSet<int>(existingUserRoles);

            foreach (var au in adminUsers)
            {
                if (!existingUserRoleSet.Contains(au.Id))
                {
                    _db.UserRoles.Add(new UserRole
                    {
                        TenantId = tenantId,
                        UserId = au.Id,
                        RoleId = adminRole.Id,
                        AssignedAt = DateTime.UtcNow,
                        AssignedBy = userId
                    });
                }
            }

            // Seed RolePermissions for Admin matching the enabled modules
            var catalogPerms = await _db.Permissions.AsNoTracking().ToListAsync();
            var existingAdminPerms = await _db.RolePermissions
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId && (r.RoleId == adminRole.Id || r.RoleName.ToLower() == "admin") && r.RevokedAt == null)
                .Select(r => r.PermissionKey)
                .ToListAsync();
            var existingAdminPermSet = new HashSet<string>(existingAdminPerms, StringComparer.OrdinalIgnoreCase);

            var enabledModCodeSet = new HashSet<string>(moduleCodes, StringComparer.OrdinalIgnoreCase);
            if (enabledModCodeSet.Contains("bilty")) enabledModCodeSet.Add("delivery_settlement");
            if (enabledModCodeSet.Contains("trips")) enabledModCodeSet.Add("trip_settlement");
            if (enabledModCodeSet.Contains("delivery_settlement")) enabledModCodeSet.Add("bilty");
            if (enabledModCodeSet.Contains("trip_settlement")) enabledModCodeSet.Add("trips");

            foreach (var perm in catalogPerms)
            {
                var mCode = ResolveModuleCode(perm.FeatureKey);
                if (mCode == null || !enabledModCodeSet.Contains(mCode)) continue;

                var baseF = perm.FeatureKey;
                var normF = EntitlementsCatalog.NormalizeFeatureKey(baseF);
                var rootF = baseF.Contains('.') ? baseF.Substring(0, baseF.IndexOf('.')) : baseF;
                var normRootF = EntitlementsCatalog.NormalizeFeatureKey(rootF);

                bool hasGranular = allFeatureKeysToPersist.Any(k =>
                    k.StartsWith(rootF + ".", StringComparison.OrdinalIgnoreCase) ||
                    k.StartsWith(normRootF + ".", StringComparison.OrdinalIgnoreCase));

                bool isParentAllowed =
                    (baseF.Equals("delivery_settlement", StringComparison.OrdinalIgnoreCase) &&
                     (allFeatureKeysToPersist.Contains("consignments") || allFeatureKeysToPersist.Contains("bilty") || allFeatureKeysToPersist.Contains("gr"))) ||
                    ((baseF.Equals("trip_settlement", StringComparison.OrdinalIgnoreCase) || baseF.Equals("empty_trips", StringComparison.OrdinalIgnoreCase)) &&
                     (allFeatureKeysToPersist.Contains("trips") || allFeatureKeysToPersist.Contains("challan") || allFeatureKeysToPersist.Contains("trip")));

                bool isPermAllowed =
                    allFeatureKeysToPersist.Contains(baseF) ||
                    allFeatureKeysToPersist.Contains(normF) ||
                    (!hasGranular && (allFeatureKeysToPersist.Contains(rootF) || allFeatureKeysToPersist.Contains(normRootF))) ||
                    isParentAllowed;

                if (!isPermAllowed)
                {
                    // Revoke if previously granted
                    var existingGrant = await _db.RolePermissions
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(r => r.TenantId == tenantId && (r.RoleId == adminRole.Id || r.RoleName.ToLower() == "admin") && r.PermissionKey == perm.Key && r.RevokedAt == null);
                    if (existingGrant != null)
                    {
                        existingGrant.RevokedAt = DateTime.UtcNow;
                    }
                    continue;
                }

                if (existingAdminPermSet.Contains(perm.Key)) continue;

                _db.RolePermissions.Add(new RolePermission
                {
                    TenantId = tenantId,
                    RoleName = "admin",
                    RoleId = adminRole.Id,
                    PermissionKey = perm.Key,
                    GrantedAt = DateTime.UtcNow,
                    GrantedBy = userId
                });
                existingAdminPermSet.Add(perm.Key);
            }

            // ---- 6. RoleOverrides (if explicitly provided in DTO) ----
            if (newDto.RoleOverrides != null)
            {
                var allTenantRoles = await _db.Roles.IgnoreQueryFilters().Where(r => r.TenantId == tenantId).ToListAsync();
                var roleIdByCode = allTenantRoles.ToDictionary(r => r.Code.ToLowerInvariant(), r => r.Id);

                var existingRolePerms = await _db.RolePermissions
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId && r.RevokedAt == null)
                    .Select(r => new { r.RoleName, r.PermissionKey })
                    .ToListAsync();
                var existingSet = existingRolePerms
                    .Select(e => $"{e.RoleName.ToLowerInvariant()}|{e.PermissionKey}")
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var kv in newDto.RoleOverrides)
                {
                    var roleName = (kv.Key ?? string.Empty).Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(roleName) || kv.Value == null) continue;

                    int? roleId = roleIdByCode.TryGetValue(roleName, out var rid) ? rid : null;

                    foreach (var featureKey in kv.Value)
                    {
                        foreach (var permKey in EntitlementsCatalog.ExpandFeatureKey(featureKey))
                        {
                            var sig = $"{roleName}|{permKey}";
                            if (existingSet.Contains(sig)) continue;
                            _db.RolePermissions.Add(new RolePermission
                            {
                                TenantId = tenantId,
                                RoleName = roleName,
                                RoleId = roleId,
                                PermissionKey = permKey,
                                GrantedAt = DateTime.UtcNow,
                                GrantedBy = userId
                            });
                            existingSet.Add(sig);
                        }
                    }
                }
            }

            // ---- 7. UserOverrides ----
            if (newDto.UserOverrides != null)
            {
                var tenantUsers = await _db.Users
                    .IgnoreQueryFilters()
                    .Where(u => u.TenantId == tenantId)
                    .Select(u => new { u.Id, u.Username })
                    .ToListAsync();
                var byId = tenantUsers.ToDictionary(u => u.Id);
                var byUsername = tenantUsers.ToDictionary(u => (u.Username ?? string.Empty).ToLowerInvariant(), u => u.Id);

                var existing = await _db.UserPermissionOverrides
                    .IgnoreQueryFilters()
                    .Where(o => o.TenantId == tenantId && o.SupersededBy == null)
                    .Select(o => new { o.UserId, o.PermissionKey })
                    .ToListAsync();
                var existingSet = existing
                    .Select(e => $"{e.UserId}|{e.PermissionKey}")
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var kv in newDto.UserOverrides)
                {
                    var key = (kv.Key ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(key) || kv.Value == null) continue;

                    int? matchedUserId = null;
                    if (int.TryParse(key, out var parsed) && byId.ContainsKey(parsed))
                    {
                        matchedUserId = parsed;
                    }
                    else if (byUsername.TryGetValue(key.ToLowerInvariant(), out var uid))
                    {
                        matchedUserId = uid;
                    }

                    if (matchedUserId == null)
                    {
                        _log.LogWarning(
                            "WriteTenantEntitlementsAsync: orphan user override tenant={TenantId} key={OrphanKey} features={Features}",
                            tenantId, key, string.Join(",", kv.Value ?? new List<string>()));
                        continue;
                    }

                    foreach (var featureKey in kv.Value)
                    {
                        foreach (var permKey in EntitlementsCatalog.ExpandFeatureKey(featureKey))
                        {
                            var sig = $"{matchedUserId.Value}|{permKey}";
                            if (existingSet.Contains(sig)) continue;

                            _db.UserPermissionOverrides.Add(new UserPermissionOverride
                            {
                                TenantId = tenantId,
                                UserId = matchedUserId.Value,
                                PermissionKey = permKey,
                                IsGranted = true,
                                GrantedAt = DateTime.UtcNow,
                                GrantedBy = userId
                            });
                            existingSet.Add(sig);
                        }
                    }
                }
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _log.LogWarning(ex, "WriteTenantEntitlementsAsync tenant={TenantId}: DB raised unique-violation; treating as idempotent re-run", tenantId);
            }
        }
    }
}
