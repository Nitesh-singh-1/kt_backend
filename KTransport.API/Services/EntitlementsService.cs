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

            // TASK-044: legacy feature-key set — stays as rollback safety per §Y.
            var tenantFeatureKeys = new HashSet<string>(
                subscription?.EnabledFeatureKeys ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            // TASK-046 Phase 1: authoritative module set from tenant_modules.
            // When tenant_modules is populated (post-backfill), use it for the
            // intersection; otherwise fall back to the legacy text[] set.
            var tenantModuleCodes = await _db.TenantModules
                .IgnoreQueryFilters()
                .Where(tm => tm.TenantId == tenantId
                             && tm.IsEnabled
                             && tm.EnabledUntil == null)
                .Join(_db.Modules.IgnoreQueryFilters(), tm => tm.ModuleId, m => m.Id, (tm, m) => m.Code)
                .ToListAsync();
            var moduleCodeSet = new HashSet<string>(tenantModuleCodes, StringComparer.OrdinalIgnoreCase);

            List<string> userGrantedPermKeys = new();
            if (userId.HasValue)
            {
                userGrantedPermKeys = await _db.UserPermissionOverrides
                    .IgnoreQueryFilters()
                    .Where(o => o.TenantId == tenantId && o.UserId == userId.Value && o.IsGranted && o.SupersededBy == null)
                    .Select(o => o.PermissionKey)
                    .ToListAsync();
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
                    .Where(r => r.TenantId == tenantId && r.RoleName == role && r.RevokedAt == null)
                    .Select(r => r.PermissionKey)
                    .ToListAsync();
            }

            var sourceKeys = userGrantedPermKeys.Count > 0 ? userGrantedPermKeys : rolePermKeys;

            foreach (var permKey in sourceKeys)
            {
                if (string.IsNullOrWhiteSpace(permKey)) continue;
                var baseFeature = ExtractBaseFeatureKey(permKey);

                // Legacy intersection — stays as rollback safety.
                if (!tenantFeatureKeys.Contains(baseFeature)) continue;

                // TASK-046 Phase 1 shim: additional intersection with
                // tenant_modules. Phase 2 will add a permissions.module_id FK;
                // until then, resolve the module via feature-key prefix.
                if (moduleCodeSet.Count > 0)
                {
                    var moduleCode = ResolveModuleCode(baseFeature);
                    if (moduleCode != null && !moduleCodeSet.Contains(moduleCode))
                    {
                        continue;
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
        private static string? ResolveModuleCode(string featureKey)
        {
            if (string.IsNullOrWhiteSpace(featureKey)) return null;
            var fk = featureKey.ToLowerInvariant();
            if (fk.StartsWith("consignments")) return "bilty";
            if (fk == "delivery_settlement") return "delivery_settlement";
            if (fk == "trip_settlement") return "trip_settlement";
            if (fk.StartsWith("empty_trips") || fk.StartsWith("trips")) return "trips";
            if (fk.StartsWith("billing")) return "billing";
            if (fk.StartsWith("pod")) return "pod";
            if (fk.StartsWith("reports")) return "reports";
            if (fk.StartsWith("master_data")) return "master_data";
            if (fk.StartsWith("quotations")) return "quotations";
            if (fk.StartsWith("vendors")) return "vendors";
            if (fk.StartsWith("claims")) return "claims";
            if (fk.StartsWith("tracking")) return "tracking";
            if (fk.StartsWith("analytics")) return "analytics";
            if (fk.StartsWith("dashboard")) return "dashboard";
            if (fk.StartsWith("system")) return "system";
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
        /// Writes the DTO's entitlements into the normalized tables. Reads the
        /// structured RoleOverrides / UserOverrides properties — the legacy
        /// RoleOverridesJson / UserOverridesJson string fields are IGNORED.
        /// </summary>
        public async Task WriteTenantEntitlementsAsync(Guid tenantId, TenantMenuEntitlementsDto newDto, int? userId)
        {
            // ---- 1. TenantEntitlementSubscription ----
            var activeSub = await _db.TenantEntitlementSubscriptions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.EffectiveUntil == null);

            if (activeSub == null)
            {
                _db.TenantEntitlementSubscriptions.Add(new TenantEntitlementSubscription
                {
                    TenantId = tenantId,
                    PlanTier = string.IsNullOrWhiteSpace(newDto.PlanTier) ? "Enterprise" : newDto.PlanTier,
                    EnabledFeatureKeys = (newDto.EnabledMenuKeys ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    EffectiveFrom = DateTime.UtcNow,
                    EffectiveUntil = null,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                });
            }
            else
            {
                activeSub.EnabledFeatureKeys = (newDto.EnabledMenuKeys ?? new List<string>())
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                activeSub.PlanTier = string.IsNullOrWhiteSpace(newDto.PlanTier) ? activeSub.PlanTier : newDto.PlanTier!;
            }

            // ---- 2. TenantReportEntitlements ----
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

            // ---- 3. RolePermissions ----
            if (newDto.RoleOverrides != null)
            {
                var existing = await _db.RolePermissions
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId && r.RevokedAt == null)
                    .Select(r => new { r.RoleName, r.PermissionKey })
                    .ToListAsync();
                var existingSet = existing
                    .Select(e => $"{e.RoleName.ToLowerInvariant()}|{e.PermissionKey}")
                    .ToHashSet();

                foreach (var kv in newDto.RoleOverrides)
                {
                    var roleName = (kv.Key ?? string.Empty).Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(roleName) || kv.Value == null) continue;

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
                                PermissionKey = permKey,
                                GrantedAt = DateTime.UtcNow,
                                GrantedBy = userId
                            });
                            existingSet.Add(sig);
                        }
                    }
                }
            }

            // ---- 4. UserPermissionOverrides ----
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
                    .ToHashSet();

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
