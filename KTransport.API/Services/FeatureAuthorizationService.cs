using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class FeatureAuthorizationService : IFeatureAuthorizationService
    {
        private readonly KTransportDbContext _context;

        public FeatureAuthorizationService(KTransportDbContext context)
        {
            _context = context;
        }

        public bool IsSuperUser(ClaimsPrincipal user)
        {
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                return false;
            }

            var role = user.FindFirst(ClaimTypes.Role)?.Value
                       ?? user.FindFirst("role")?.Value
                       ?? string.Empty;

            return string.Equals(role, "SUPER_USER", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "tenantadmin", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "superadmin", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "TENANT_OWNER", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsPlatformAdmin(ClaimsPrincipal user)
        {
            if (!IsSuperUser(user))
            {
                return false;
            }

            var tenantClaim = user.FindFirst("tenant_id")?.Value ?? user.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantClaim, out var tenantId) && tenantId == TenantContext.DefaultTenantId;
        }

        /// <summary>
        /// TASK-044 Phase 3: subscribed features now come from
        /// tenant_entitlement_subscriptions, not the dropped menu_entitlements_json.
        /// </summary>
        public async Task<HashSet<string>> GetSubscribedFeaturesAsync(Guid tenantId)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Read from canonical tenant_modules table (TASK-049)
            var tenantModules = await (from tm in _context.TenantModules.IgnoreQueryFilters()
                                       join m in _context.Modules.IgnoreQueryFilters() on tm.ModuleId equals m.Id
                                       where tm.TenantId == tenantId && tm.EnabledUntil == null
                                       select m.Code).ToListAsync();

            foreach (var modCode in tenantModules)
            {
                result.Add(modCode);
                var canonical = FeatureConstants.Normalize(modCode);
                if (!string.IsNullOrWhiteSpace(canonical))
                {
                    result.Add(canonical);
                }
            }

            // 2. Read from tenant_entitlement_subscriptions table
            var subscription = await _context.TenantEntitlementSubscriptions
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId && s.EffectiveUntil == null)
                .OrderByDescending(s => s.EffectiveFrom)
                .FirstOrDefaultAsync();

            if (subscription != null && subscription.EnabledFeatureKeys != null && subscription.EnabledFeatureKeys.Count > 0)
            {
                foreach (var key in subscription.EnabledFeatureKeys)
                {
                    result.Add(key);
                    var canonical = FeatureConstants.Normalize(key);
                    if (!string.IsNullOrWhiteSpace(canonical))
                    {
                        result.Add(canonical);
                    }
                }
            }

            if (result.Count > 0)
            {
                return result;
            }

            // Default fallback: Starter/Enterprise features
            foreach (var f in FeatureConstants.AllFeatures)
            {
                result.Add(f);
            }
            return result;
        }

        /// <summary>
        /// TASK-044 Phase 3: user+role effective grants now come from
        /// role_permissions / user_permission_overrides.
        /// </summary>
        public async Task<HashSet<string>> GetEffectiveFeaturesForUserAsync(Guid tenantId, string userRole, string userIdOrName)
        {
            var subscribedFeatures = await GetSubscribedFeaturesAsync(tenantId);
            bool isSuper = string.Equals(userRole, "SUPER_USER", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(userRole, "admin", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(userRole, "tenantadmin", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(userRole, "superadmin", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(userRole, "TENANT_OWNER", StringComparison.OrdinalIgnoreCase);

            if (isSuper)
            {
                var superSet = new HashSet<string>(subscribedFeatures, StringComparer.OrdinalIgnoreCase)
                {
                    FeatureConstants.SAAS_CONFIGURATION,
                    FeatureConstants.USER_MANAGEMENT,
                    "DASHBOARD",
                    "dashboard"
                };
                return superSet;
            }

            // Resolve userId from id-or-username.
            int? userId = null;
            if (!string.IsNullOrWhiteSpace(userIdOrName))
            {
                if (int.TryParse(userIdOrName, out var parsed))
                {
                    userId = parsed;
                }
                else
                {
                    var row = await _context.Users
                        .IgnoreQueryFilters()
                        .Where(u => u.TenantId == tenantId && u.Username == userIdOrName)
                        .Select(u => (int?)u.Id)
                        .FirstOrDefaultAsync();
                    userId = row;
                }
            }

            var userAllowedRaw = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. User overrides take precedence.
            List<string> userGrants = new();
            if (userId.HasValue)
            {
                userGrants = await _context.UserPermissionOverrides
                    .IgnoreQueryFilters()
                    .Where(o => o.TenantId == tenantId && o.UserId == userId.Value && o.IsGranted && o.SupersededBy == null)
                    .Select(o => o.PermissionKey)
                    .ToListAsync();
            }

            // 2. Role grants are the fallback.
            List<string> roleGrants = new();
            if (!string.IsNullOrWhiteSpace(userRole))
            {
                roleGrants = await _context.RolePermissions
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId && r.RoleName == userRole && r.RevokedAt == null)
                    .Select(r => r.PermissionKey)
                    .ToListAsync();
            }

            var source = userGrants.Count > 0 ? userGrants : roleGrants;
            foreach (var permKey in source)
            {
                if (string.IsNullOrWhiteSpace(permKey)) continue;
                userAllowedRaw.Add(permKey);
                // Also add the base feature key so legacy feature-level checks still match.
                var idx = permKey.LastIndexOf('.');
                if (idx > 0)
                {
                    var tail = permKey.Substring(idx + 1).ToLowerInvariant();
                    if (tail is "view" or "create" or "edit" or "delete" or "print" or "approve" or "export")
                    {
                        var baseKey = permKey.Substring(0, idx);
                        userAllowedRaw.Add(baseKey);
                        var canon = FeatureConstants.Normalize(baseKey);
                        if (!string.IsNullOrWhiteSpace(canon)) userAllowedRaw.Add(canon);
                    }
                }
                var canonFull = FeatureConstants.Normalize(permKey);
                if (!string.IsNullOrWhiteSpace(canonFull)) userAllowedRaw.Add(canonFull);
            }

            // Sub-user with NO explicit grants gets ONLY the dashboard.
            if (userAllowedRaw.Count == 0)
            {
                userAllowedRaw.Add("dashboard");
            }

            userAllowedRaw.Add("dashboard");
            userAllowedRaw.Add("DASHBOARD");

            // Effective Access = SubscribedFeatures ∩ UserAssignedPermissions
            var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in userAllowedRaw)
            {
                var canonical = FeatureConstants.Normalize(item);
                if (subscribedFeatures.Contains(item) || subscribedFeatures.Contains(canonical))
                {
                    effective.Add(item);
                    if (!string.IsNullOrWhiteSpace(canonical)) effective.Add(canonical);
                }
            }

            return effective;
        }

        public async Task<FeatureAuthorizationResult> AuthorizeFeatureAsync(ClaimsPrincipal user, Guid tenantId, string featureCode)
        {
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                return FeatureAuthorizationResult.Unauthorized("Authentication is required to access this resource.");
            }

            var canonicalRequested = FeatureConstants.Normalize(featureCode);

            if (canonicalRequested == FeatureConstants.SAAS_CONFIGURATION)
            {
                if (IsSuperUser(user))
                {
                    return FeatureAuthorizationResult.Success();
                }
                return FeatureAuthorizationResult.Forbidden("Access denied: SaaS Configuration is restricted to Organization Super Users.");
            }

            var subscribedFeatures = await GetSubscribedFeaturesAsync(tenantId);
            bool isSubscribed = subscribedFeatures.Contains(featureCode) || subscribedFeatures.Contains(canonicalRequested);

            if (!isSubscribed)
            {
                return FeatureAuthorizationResult.Forbidden($"Access denied: Organization is not subscribed to module '{featureCode}'.");
            }

            if (IsSuperUser(user))
            {
                return FeatureAuthorizationResult.Success();
            }

            var role = user.FindFirst(ClaimTypes.Role)?.Value
                       ?? user.FindFirst("role")?.Value
                       ?? "SUB_USER";

            var username = user.FindFirst(ClaimTypes.Name)?.Value
                           ?? user.FindFirst("username")?.Value;

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? user.FindFirst("sub")?.Value;

            var effectiveFeatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(username))
            {
                var permsByName = await GetEffectiveFeaturesForUserAsync(tenantId, role, username);
                foreach (var p in permsByName) effectiveFeatures.Add(p);
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var permsById = await GetEffectiveFeaturesForUserAsync(tenantId, role, userId);
                foreach (var p in permsById) effectiveFeatures.Add(p);
            }

            if (effectiveFeatures.Contains(featureCode) || effectiveFeatures.Contains(canonicalRequested))
            {
                return FeatureAuthorizationResult.Success();
            }

            return FeatureAuthorizationResult.Forbidden($"Access denied: Sub-user '{username ?? userId}' has not been granted permission for module '{featureCode}'.");
        }
    }
}
