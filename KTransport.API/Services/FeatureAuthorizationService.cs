using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class FeatureAuthorizationService : IFeatureAuthorizationService
    {
        private readonly KTransportDbContext _context;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

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

        public async Task<HashSet<string>> GetSubscribedFeaturesAsync(Guid tenantId)
        {
            var setting = await _context.TenantSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (setting != null && !string.IsNullOrWhiteSpace(setting.MenuEntitlementsJson) && setting.MenuEntitlementsJson != "{}")
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<TenantMenuEntitlementsDto>(setting.MenuEntitlementsJson, JsonOptions);
                    if (parsed?.EnabledMenuKeys != null && parsed.EnabledMenuKeys.Count > 0)
                    {
                        foreach (var key in parsed.EnabledMenuKeys)
                        {
                            result.Add(key);
                            var canonical = FeatureConstants.Normalize(key);
                            if (!string.IsNullOrWhiteSpace(canonical))
                            {
                                result.Add(canonical);
                            }
                        }
                        return result;
                    }
                }
                catch
                {
                    // Fallback to defaults
                }
            }

            // Default fallback: Starter/Enterprise features
            foreach (var f in FeatureConstants.AllFeatures)
            {
                result.Add(f);
            }
            return result;
        }

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
                // Super User gets all organization-subscribed features + admin features
                var superSet = new HashSet<string>(subscribedFeatures, StringComparer.OrdinalIgnoreCase)
                {
                    FeatureConstants.SAAS_CONFIGURATION,
                    FeatureConstants.USER_MANAGEMENT,
                    "DASHBOARD",
                    "dashboard"
                };
                return superSet;
            }

            // Sub User: Look up User-Specific Overrides from TenantSettings
            var setting = await _context.TenantSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            var userAllowedRaw = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (setting != null && !string.IsNullOrWhiteSpace(setting.MenuEntitlementsJson))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<TenantMenuEntitlementsDto>(setting.MenuEntitlementsJson, JsonOptions);
                    if (parsed != null && !string.IsNullOrWhiteSpace(parsed.UserOverridesJson))
                    {
                        var userOverrides = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(parsed.UserOverridesJson, JsonOptions);
                        if (userOverrides != null)
                        {
                            var matchingKey = userOverrides.Keys.FirstOrDefault(k => 
                                string.Equals(k, userIdOrName, StringComparison.OrdinalIgnoreCase));

                            if (matchingKey != null && userOverrides[matchingKey] != null)
                            {
                                foreach (var k in userOverrides[matchingKey])
                                {
                                    userAllowedRaw.Add(k);
                                    var canon = FeatureConstants.Normalize(k);
                                    if (!string.IsNullOrWhiteSpace(canon)) userAllowedRaw.Add(canon);
                                }
                            }
                        }
                    }

                    // If no direct user override, check role override
                    if (userAllowedRaw.Count == 0 && parsed != null && !string.IsNullOrWhiteSpace(parsed.RoleOverridesJson))
                    {
                        var roleOverrides = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(parsed.RoleOverridesJson, JsonOptions);
                        if (roleOverrides != null)
                        {
                            var matchingRole = roleOverrides.Keys.FirstOrDefault(k => 
                                string.Equals(k, userRole, StringComparison.OrdinalIgnoreCase));

                            if (matchingRole != null && roleOverrides[matchingRole] != null)
                            {
                                foreach (var k in roleOverrides[matchingRole])
                                {
                                    userAllowedRaw.Add(k);
                                    var canon = FeatureConstants.Normalize(k);
                                    if (!string.IsNullOrWhiteSpace(canon)) userAllowedRaw.Add(canon);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore parse errors
                }
            }

            // If no explicit user override and no role override, default sub-users to standard operational subscribed features
            if (userAllowedRaw.Count == 0)
            {
                foreach (var k in subscribedFeatures)
                {
                    var canon = FeatureConstants.Normalize(k);
                    if (canon != FeatureConstants.SAAS_CONFIGURATION && canon != FeatureConstants.USER_MANAGEMENT && canon != "CLIENTS" && canon != "SYSTEM")
                    {
                        userAllowedRaw.Add(k);
                        if (!string.IsNullOrWhiteSpace(canon)) userAllowedRaw.Add(canon);
                    }
                }
            }

            // Always allow dashboard for active sub-users
            userAllowedRaw.Add("dashboard");
            userAllowedRaw.Add("DASHBOARD");

            // Effective Access = SubscribedFeatures ∩ UserAssignedPermissions
            // A user can NEVER receive a feature that the organization has not subscribed to!
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

            // 1. SaaS Configuration is STRICTLY reserved for Super User
            if (canonicalRequested == FeatureConstants.SAAS_CONFIGURATION)
            {
                if (IsSuperUser(user))
                {
                    return FeatureAuthorizationResult.Success();
                }
                return FeatureAuthorizationResult.Forbidden("Access denied: SaaS Configuration is restricted to Organization Super Users.");
            }

            // 2. Check Organization Subscription Level
            var subscribedFeatures = await GetSubscribedFeaturesAsync(tenantId);
            bool isSubscribed = subscribedFeatures.Contains(featureCode) || subscribedFeatures.Contains(canonicalRequested);

            if (!isSubscribed)
            {
                return FeatureAuthorizationResult.Forbidden($"Access denied: Organization is not subscribed to module '{featureCode}'.");
            }

            // 3. Super User gets access to any feature subscribed by their organization
            if (IsSuperUser(user))
            {
                return FeatureAuthorizationResult.Success();
            }

            // 4. Sub User: Verify User-level permission (checking by username, userId, or name claim)
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
