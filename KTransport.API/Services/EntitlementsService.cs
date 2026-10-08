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

            var tenantFeatureKeys = new HashSet<string>(
                subscription?.EnabledFeatureKeys ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            List<string> userGrantedPermKeys = new();
            if (userId.HasValue)
            {
                userGrantedPermKeys = await _db.UserPermissionOverrides
                    .IgnoreQueryFilters()
                    .Where(o => o.TenantId == tenantId && o.UserId == userId.Value && o.IsGranted && o.SupersededBy == null)
                    .Select(o => o.PermissionKey)
                    .ToListAsync();
            }

            List<string> rolePermKeys = new();
            if (!string.IsNullOrWhiteSpace(role))
            {
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
                if (tenantFeatureKeys.Contains(baseFeature))
                {
                    effective.Add(permKey);
                }
            }

            return effective;
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
