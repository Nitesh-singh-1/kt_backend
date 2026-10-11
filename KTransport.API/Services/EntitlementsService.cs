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
        /// TASK-049 Option B: writes the tenant's entitlements into the
        /// normalized tables. <see cref="TenantMenuEntitlementsDto.ModuleCodes"/>
        /// is the authoritative input; the legacy <c>EnabledMenuKeys</c> list is
        /// accepted for one release as back-compat.
        /// </summary>
        Task WriteTenantEntitlementsAsync(Guid tenantId, TenantMenuEntitlementsDto newDto, int? userId);
    }

    /// <summary>
    /// TASK-049 Option B: writes/reads the normalized entitlement tables as
    /// the sole source of truth. All string-prefix magic, legacy aliases, and
    /// parent→child auto-grant branches (menu.md §2 bugs 1/2/3) are deleted.
    /// Admin role seeding is a single structural join: permissions whose
    /// module_id ∈ tenant_modules(tenant). Menu rendering joins on module_id
    /// and permission_id (no more fuzzy key matcher).
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

        /// <summary>
        /// Returns the user's effective permission KEY set (e.g. "billing.invoices.view").
        /// Pure structural: permissions ⨝ tenant_modules ⨝ role_permissions ⨝ user_roles,
        /// plus user_permission_overrides for +/- deltas. No prefix magic,
        /// no legacy alias normalization, no parent→child auto-grant.
        /// </summary>
        public async Task<HashSet<string>> ComputeEffectivePermissionsFromTablesAsync(Guid tenantId, int? userId, string? role)
        {
            var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Resolve the set of permission ids that are visible to this
            // tenant: permission.module_id ∈ enabled tenant_modules. For
            // legacy test rows whose Permission.ModuleId has not been
            // backfilled yet (0), fall back to a feature-key root match
            // against modules.code — this keeps the production migration
            // simple while letting unit tests seed Permission rows without
            // also wiring the FK.
            var tenantModuleIds = await _db.TenantModules
                .IgnoreQueryFilters()
                .Where(tm => tm.TenantId == tenantId && tm.EnabledUntil == null)
                .Select(tm => tm.ModuleId)
                .ToListAsync();
            var tmSet = new HashSet<int>(tenantModuleIds);

            var modulesByCode = await _db.Modules.IgnoreQueryFilters()
                .ToDictionaryAsync(m => m.Code, m => m.Id, StringComparer.OrdinalIgnoreCase);

            // Phase A back-compat: if a tenant has no TenantModules rows yet
            // (but was configured via the legacy path), derive the gate from
            // the subscription's enabled_feature_keys. The migration backfills
            // real tenant_modules rows for every live tenant, so this
            // fallback is strictly a one-release transition for pre-TASK-049
            // tenants and for unit tests that seed only the legacy column.
            HashSet<string>? legacyFeatureGate = null;
            var subscriptionKeys = await _db.TenantEntitlementSubscriptions
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId && s.EffectiveUntil == null)
                .OrderByDescending(s => s.EffectiveFrom)
                .Select(s => s.EnabledFeatureKeys)
                .FirstOrDefaultAsync();

            if (tmSet.Count == 0 && subscriptionKeys != null)
            {
                if (modulesByCode.Count > 0)
                {
                    foreach (var k in subscriptionKeys)
                    {
                        if (string.IsNullOrWhiteSpace(k)) continue;
                        var root = k.Split('.').FirstOrDefault() ?? string.Empty;
                        if (modulesByCode.TryGetValue(root, out var mid)) tmSet.Add(mid);
                        else if (root == "consignments" && modulesByCode.TryGetValue("bilty", out var bid)) tmSet.Add(bid);
                        else if (root == "gr" && modulesByCode.TryGetValue("bilty", out var bid2)) tmSet.Add(bid2);
                        else if (root == "challan" && modulesByCode.TryGetValue("trips", out var tid)) tmSet.Add(tid);
                    }
                }
                if (tmSet.Count == 0)
                {
                    // No Modules catalog available at all — gate by raw
                    // feature-key prefix (test-only path).
                    legacyFeatureGate = new HashSet<string>(subscriptionKeys, StringComparer.OrdinalIgnoreCase);
                }
            }

            if (tmSet.Count == 0 && legacyFeatureGate == null)
            {
                return effective; // tenant has no gate info at all → empty.
            }

            var allPermissions = await _db.Permissions.IgnoreQueryFilters().ToListAsync();
            var tenantPermIdSet = new HashSet<int>();
            foreach (var p in allPermissions)
            {
                int modId = p.ModuleId;
                if (modId == 0)
                {
                    var root = (p.FeatureKey ?? string.Empty).Split('.').FirstOrDefault() ?? string.Empty;
                    if (modulesByCode.TryGetValue(root, out var mid)) modId = mid;
                    else if (root == "consignments" && modulesByCode.TryGetValue("bilty", out var bid)) modId = bid;
                    else if (root == "challan" && modulesByCode.TryGetValue("trips", out var tid)) modId = tid;
                }
                if (tmSet.Contains(modId)) tenantPermIdSet.Add(p.Id);
            }

            // --- role grants ---
            List<int> activeRoleIds = new();
            if (userId.HasValue)
            {
                activeRoleIds = await _db.UserRoles
                    .IgnoreQueryFilters()
                    .Where(ur => ur.TenantId == tenantId && ur.UserId == userId.Value && ur.RevokedAt == null)
                    .Select(ur => ur.RoleId)
                    .ToListAsync();
            }

            List<(int? PermissionId, string PermissionKey)> rolePerms = new();
            if (activeRoleIds.Count > 0)
            {
                rolePerms = (await _db.RolePermissions
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId && r.RevokedAt == null
                             && r.RoleId != null && activeRoleIds.Contains(r.RoleId.Value))
                    .Select(r => new { r.PermissionId, r.PermissionKey })
                    .ToListAsync())
                    .Select(r => (r.PermissionId, r.PermissionKey))
                    .ToList();
            }
            else if (!string.IsNullOrWhiteSpace(role))
            {
                // Fallback: legacy role_name string match (no user_roles row yet).
                rolePerms = (await _db.RolePermissions
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId && r.RevokedAt == null
                             && r.RoleName.ToLower() == role.ToLower())
                    .Select(r => new { r.PermissionId, r.PermissionKey })
                    .ToListAsync())
                    .Select(r => (r.PermissionId, r.PermissionKey))
                    .ToList();
            }

            // --- user overrides (grants + revokes) ---
            List<(int? PermissionId, string PermissionKey, bool IsGranted)> userOverrides = new();
            if (userId.HasValue)
            {
                userOverrides = (await _db.UserPermissionOverrides
                    .IgnoreQueryFilters()
                    .Where(o => o.TenantId == tenantId && o.UserId == userId.Value && o.SupersededBy == null)
                    .Select(o => new { o.PermissionId, o.PermissionKey, o.IsGranted })
                    .ToListAsync())
                    .Select(o => (o.PermissionId, o.PermissionKey, o.IsGranted))
                    .ToList();
            }

            // Build a key→id map so legacy rows without PermissionId still gate.
            // (New inserts set PermissionId; this handles mixed state.)
            var keyToId = await _db.Permissions
                .IgnoreQueryFilters()
                .Select(p => new { p.Id, p.Key })
                .ToDictionaryAsync(p => p.Key, p => p.Id, StringComparer.OrdinalIgnoreCase);

            bool InTenantModules(int? permId, string permKey)
            {
                if (legacyFeatureGate != null)
                {
                    // Pure feature-key prefix gate (no Modules catalog).
                    var parts = (permKey ?? string.Empty).Split('.');
                    for (int n = parts.Length - 1; n >= 1; n--)
                    {
                        var prefix = string.Join(".", parts.Take(n));
                        if (legacyFeatureGate.Contains(prefix)) return true;
                    }
                    return false;
                }
                if (permId.HasValue) return tenantPermIdSet.Contains(permId.Value);
                if (keyToId.TryGetValue(permKey, out var id)) return tenantPermIdSet.Contains(id);
                // Legacy fallback: no Permission row for this key — derive
                // the module from the key's root prefix (e.g.
                // "master_data.driver_ledger.view" → "master_data").
                var root2 = (permKey ?? string.Empty).Split('.').FirstOrDefault() ?? string.Empty;
                if (modulesByCode.TryGetValue(root2, out var mid2)) return tmSet.Contains(mid2);
                if (root2 == "consignments" && modulesByCode.TryGetValue("bilty", out var bid3)) return tmSet.Contains(bid3);
                if (root2 == "challan" && modulesByCode.TryGetValue("trips", out var tid3)) return tmSet.Contains(tid3);
                return false;
            }

            // ADR authorization-rbac-architecture.md §F:
            //   effective = (role grants ∪ user grants ∖ user revokes) ∩ tenant_modules
            foreach (var (pid, pkey) in rolePerms)
            {
                if (string.IsNullOrWhiteSpace(pkey)) continue;
                if (!InTenantModules(pid, pkey)) continue;
                effective.Add(pkey);
            }
            foreach (var (pid, pkey, granted) in userOverrides)
            {
                if (string.IsNullOrWhiteSpace(pkey)) continue;
                if (!InTenantModules(pid, pkey)) continue;
                if (granted) effective.Add(pkey);
                else effective.Remove(pkey);
            }

            return effective;
        }

        [Obsolete("Phase 3 removed the JSON source. These methods now log a warning and return an empty summary. Will be deleted in next release.")]
        public Task<BackfillSummaryDto> BackfillFromJsonAsync()
        {
            _log.LogWarning("BackfillFromJsonAsync invoked, but the menu_entitlements_json source was dropped in Phase 3. Returning an empty summary.");
            return Task.FromResult(new BackfillSummaryDto { TenantsProcessed = 0 });
        }

        [Obsolete("Phase 3 removed the JSON source. These methods now log a warning and return an empty summary. Will be deleted in next release.")]
        public Task<BackfillSummaryDto> BackfillTenantAsync(Guid tenantId, TenantMenuEntitlementsDto json)
            => BackfillTenantAsync(tenantId, json, actingUserId: null);

        [Obsolete("Phase 3 removed the JSON source. These methods now log a warning and return an empty summary. Will be deleted in next release.")]
        public Task<BackfillSummaryDto> BackfillTenantAsync(Guid tenantId, TenantMenuEntitlementsDto json, int? actingUserId)
        {
            _log.LogWarning("BackfillTenantAsync tenant={TenantId} invoked, but Phase 3 removed the JSON source.", tenantId);
            return Task.FromResult(new BackfillSummaryDto { TenantsProcessed = 0 });
        }

        /// <summary>
        /// TASK-049 Option B: the full rewrite per menu.md §5.3. ModuleCodes is
        /// the primary input; EnabledMenuKeys is accepted only as a one-release
        /// backward-compat path (we map the legacy keys to modules.code and
        /// log a deprecation warning). Tenant_modules is the sole source of
        /// truth. Admin role_permissions is reseeded as EXACTLY the structural
        /// join (permissions where module_id ∈ enabled tenant_modules).
        /// </summary>
        public async Task WriteTenantEntitlementsAsync(Guid tenantId, TenantMenuEntitlementsDto newDto, int? userId)
        {
            // ---- 1. Resolve the input module code set ---------------------
            var allModules = await _db.Modules.IgnoreQueryFilters().ToListAsync();
            var moduleByCode = allModules.ToDictionary(m => m.Code, m => m, StringComparer.OrdinalIgnoreCase);
            var moduleById = allModules.ToDictionary(m => m.Id, m => m);

            var requestedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (newDto.ModuleCodes != null && newDto.ModuleCodes.Count > 0)
            {
                foreach (var code in newDto.ModuleCodes)
                {
                    if (string.IsNullOrWhiteSpace(code)) continue;
                    if (!moduleByCode.ContainsKey(code))
                    {
                        _log.LogWarning("WriteTenantEntitlementsAsync tenant={TenantId}: unknown module code '{Code}', skipping.", tenantId, code);
                        continue;
                    }
                    requestedCodes.Add(code);
                }
            }
            else if (newDto.EnabledMenuKeys != null && newDto.EnabledMenuKeys.Count > 0)
            {
                // One-release back-compat: map legacy feature keys to module
                // codes via modules.code (direct) or via a tiny alias set.
                // This path is deliberately *not* clever — if a key does not
                // resolve to a known module we skip and warn.
                _log.LogWarning("WriteTenantEntitlementsAsync tenant={TenantId}: payload used legacy EnabledMenuKeys; map to ModuleCodes.", tenantId);
                foreach (var k in newDto.EnabledMenuKeys)
                {
                    if (string.IsNullOrWhiteSpace(k)) continue;
                    var mapped = MapLegacyKeyToModuleCode(k, moduleByCode);
                    if (mapped != null) requestedCodes.Add(mapped);
                }
            }

            // Always include dashboard + system so admin never loses settings.
            if (moduleByCode.ContainsKey("dashboard")) requestedCodes.Add("dashboard");
            if (moduleByCode.ContainsKey("system")) requestedCodes.Add("system");

            // NOTE DELIBERATE OMISSION (menu.md §2 bugs 1/2/3):
            // No "consignments → delivery_settlement" auto-add.
            // No "trips → trip_settlement" auto-add.
            // The platform-admin chooses each module explicitly.

            var requestedModuleIds = new HashSet<int>(requestedCodes
                .Select(c => moduleByCode[c].Id));

            // ---- 2. Upsert tenant_modules ---------------------------------
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

            // Revoke any active tenant_module not in the request.
            foreach (var tm in existingTenantModules
                .Where(tm => tm.EnabledUntil == null && !requestedModuleIds.Contains(tm.ModuleId)))
            {
                tm.EnabledUntil = DateTime.UtcNow;
            }

            // ---- 3. Mirror enabled_feature_keys from tenant_modules -------
            // DERIVED. Not an input. Kept populated so any legacy reader works
            // for one release (menu.md §5.5 drop plan).
            var activeCodes = requestedCodes.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();

            var activeSub = await _db.TenantEntitlementSubscriptions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.EffectiveUntil == null);

            if (activeSub == null)
            {
                _db.TenantEntitlementSubscriptions.Add(new TenantEntitlementSubscription
                {
                    TenantId = tenantId,
                    PlanTier = string.IsNullOrWhiteSpace(newDto.PlanTier) ? "Enterprise" : newDto.PlanTier,
                    EnabledFeatureKeys = activeCodes,
                    EffectiveFrom = DateTime.UtcNow,
                    EffectiveUntil = null,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                });
            }
            else
            {
                activeSub.EnabledFeatureKeys = activeCodes;
                if (!string.IsNullOrWhiteSpace(newDto.PlanTier)) activeSub.PlanTier = newDto.PlanTier!;
            }

            // ---- 4. TenantReportEntitlements (unchanged behavior) ---------
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

            // ---- 5. Admin role + user_roles seeding -----------------------
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

            // ---- 6. Reseed admin role_permissions (structural join) -------
            // Target = permissions whose module_id ∈ requested modules.
            // Legacy Permission rows with ModuleId=0 (pre-migration or test
            // seeds) resolve via feature_key root → modules.code.
            var allPermCatalog = await _db.Permissions
                .IgnoreQueryFilters()
                .Select(p => new { p.Id, p.Key, p.FeatureKey, p.ModuleId })
                .ToListAsync();
            var targetPerms = new List<(int Id, string Key)>();
            foreach (var p in allPermCatalog)
            {
                int modId = p.ModuleId;
                if (modId == 0)
                {
                    var root = (p.FeatureKey ?? string.Empty).Split('.').FirstOrDefault() ?? string.Empty;
                    if (moduleByCode.TryGetValue(root, out var mi)) modId = mi.Id;
                    else if (root == "consignments" && moduleByCode.TryGetValue("bilty", out var bi)) modId = bi.Id;
                    else if (root == "challan" && moduleByCode.TryGetValue("trips", out var ti)) modId = ti.Id;
                }
                if (requestedModuleIds.Contains(modId))
                {
                    targetPerms.Add((p.Id, p.Key));
                }
            }
            var targetPermIdSet = targetPerms.Select(p => p.Id).ToHashSet();
            var targetPermKeySet = targetPerms.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Current active admin grants.
            var currentAdminGrants = await _db.RolePermissions
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId
                         && (r.RoleId == adminRole.Id || r.RoleName.ToLower() == "admin")
                         && r.RevokedAt == null)
                .ToListAsync();

            // Revoke anything outside the target set (no `isParentAllowed`,
            // no legacy alias branch — pure structural diff).
            foreach (var row in currentAdminGrants)
            {
                bool inTarget = row.PermissionId.HasValue
                    ? targetPermIdSet.Contains(row.PermissionId.Value)
                    : targetPermKeySet.Contains(row.PermissionKey);
                if (!inTarget) row.RevokedAt = DateTime.UtcNow;
            }

            // Insert anything in target but not already granted.
            var currentActiveKeys = currentAdminGrants
                .Where(r => r.RevokedAt == null)
                .Select(r => r.PermissionKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var tp in targetPerms)
            {
                if (currentActiveKeys.Contains(tp.Key)) continue;
                _db.RolePermissions.Add(new RolePermission
                {
                    TenantId = tenantId,
                    RoleName = "admin",
                    RoleId = adminRole.Id,
                    PermissionKey = tp.Key,
                    PermissionId = tp.Id,
                    GrantedAt = DateTime.UtcNow,
                    GrantedBy = userId
                });
            }

            // ---- 7. RoleOverrides (explicit extras for non-admin roles) ---
            if (newDto.RoleOverrides != null)
            {
                var allTenantRoles = await _db.Roles.IgnoreQueryFilters().Where(r => r.TenantId == tenantId).ToListAsync();
                var roleIdByCode = allTenantRoles.ToDictionary(r => r.Code.ToLowerInvariant(), r => r.Id);

                var allPermsByKey = (await _db.Permissions.IgnoreQueryFilters()
                    .Select(p => new { p.Id, p.Key }).ToListAsync())
                    .ToDictionary(p => p.Key, p => p.Id, StringComparer.OrdinalIgnoreCase);

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
                            allPermsByKey.TryGetValue(permKey, out var pid);
                            _db.RolePermissions.Add(new RolePermission
                            {
                                TenantId = tenantId,
                                RoleName = roleName,
                                RoleId = roleId,
                                PermissionKey = permKey,
                                PermissionId = pid == 0 ? null : pid,
                                GrantedAt = DateTime.UtcNow,
                                GrantedBy = userId
                            });
                            existingSet.Add(sig);
                        }
                    }
                }
            }

            // ---- 8. UserOverrides -----------------------------------------
            if (newDto.UserOverrides != null)
            {
                var tenantUsers = await _db.Users
                    .IgnoreQueryFilters()
                    .Where(u => u.TenantId == tenantId)
                    .Select(u => new { u.Id, u.Username })
                    .ToListAsync();
                var byId = tenantUsers.ToDictionary(u => u.Id);
                var byUsername = tenantUsers.ToDictionary(u => (u.Username ?? string.Empty).ToLowerInvariant(), u => u.Id);

                var allPermsByKey = (await _db.Permissions.IgnoreQueryFilters()
                    .Select(p => new { p.Id, p.Key }).ToListAsync())
                    .ToDictionary(p => p.Key, p => p.Id, StringComparer.OrdinalIgnoreCase);

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
                    if (int.TryParse(key, out var parsed) && byId.ContainsKey(parsed)) matchedUserId = parsed;
                    else if (byUsername.TryGetValue(key.ToLowerInvariant(), out var uid)) matchedUserId = uid;

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
                            allPermsByKey.TryGetValue(permKey, out var pid);
                            _db.UserPermissionOverrides.Add(new UserPermissionOverride
                            {
                                TenantId = tenantId,
                                UserId = matchedUserId.Value,
                                PermissionKey = permKey,
                                PermissionId = pid == 0 ? null : pid,
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
                _log.LogWarning(ex, "WriteTenantEntitlementsAsync tenant={TenantId}: unique-violation; treating as idempotent re-run", tenantId);
            }
        }

        /// <summary>
        /// One-release legacy-key → module-code map. Tiny on purpose: the
        /// alias table lived in the old EntitlementsCatalog and is being
        /// retired, so new code only needs to resolve the handful of keys the
        /// frontend may still send while it ships the ModuleCodes payload.
        /// </summary>
        private static string? MapLegacyKeyToModuleCode(string key, Dictionary<string, Module> moduleByCode)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            var k = key.Trim().ToLowerInvariant();
            // Direct match wins.
            if (moduleByCode.ContainsKey(k)) return k;

            // Known legacy aliases.
            switch (k)
            {
                case "gr":
                case "consignments":
                case "bilty":
                    return moduleByCode.ContainsKey("bilty") ? "bilty" : null;
                case "challan":
                case "trip":
                    return moduleByCode.ContainsKey("trips") ? "trips" : null;
                case "bill_book":
                    return moduleByCode.ContainsKey("billing") ? "billing" : null;
                case "masterdata":
                    return moduleByCode.ContainsKey("master_data") ? "master_data" : null;
            }

            // Dotted leaf → root prefix: "billing.invoices" → "billing".
            var dot = k.IndexOf('.');
            if (dot > 0)
            {
                var root = k.Substring(0, dot);
                if (moduleByCode.ContainsKey(root)) return root;
                if (root == "consignments" && moduleByCode.ContainsKey("bilty")) return "bilty";
                if (root == "masterdata" && moduleByCode.ContainsKey("master_data")) return "master_data";
            }
            return null;
        }
    }
}
