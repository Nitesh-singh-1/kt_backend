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
    /// <summary>
    /// RBAC clean-slate (ADR rbac-menu-consolidation-clean-slate): the legacy
    /// feature-flag engine (FeatureConstants.Normalize + enabled_feature_keys +
    /// alias maps) is retired. "Subscribed features" are now the tenant's active
    /// module codes; effective per-user access is the single structural engine
    /// (<see cref="IEntitlementsService"/>). [RequireFeature] resolves a module
    /// code against tenant_modules directly.
    /// </summary>
    public class FeatureAuthorizationService : IFeatureAuthorizationService
    {
        private readonly KTransportDbContext _context;
        private readonly IEntitlementsService _entitlements;

        public FeatureAuthorizationService(KTransportDbContext context, IEntitlementsService entitlements)
        {
            _context = context;
            _entitlements = entitlements;
        }

        public bool IsSuperUser(ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true) return false;

            // Authoritative: the is_super_user claim resolved from DB grants at token issue.
            var flag = user.FindFirst("is_super_user")?.Value;
            if (!string.IsNullOrEmpty(flag))
                return string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);

            // Fallback for tokens minted before the claim existed.
            var role = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value ?? string.Empty;
            return RbacRoles.IsAdminRole(role);
        }

        public bool IsPlatformAdmin(ClaimsPrincipal user)
        {
            if (!IsSuperUser(user)) return false;
            var tenantClaim = user.FindFirst("tenant_id")?.Value ?? user.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantClaim, out var tenantId) && tenantId == TenantContext.DefaultTenantId;
        }

        /// <summary>The tenant's active module codes (canonical). No aliases, no feature keys.</summary>
        public Task<HashSet<string>> GetSubscribedFeaturesAsync(Guid tenantId)
            => _entitlements.GetActiveModuleCodesAsync(tenantId);

        /// <summary>
        /// The user's effective permission keys from the single structural engine.
        /// Admins (super user) additionally carry the admin management keys so
        /// display surfaces stay consistent with the menu.
        /// </summary>
        public async Task<HashSet<string>> GetEffectiveFeaturesForUserAsync(Guid tenantId, string userRole, string userIdOrName)
        {
            if (string.Equals(userRole, RbacRoles.PlatformSuperUser, StringComparison.OrdinalIgnoreCase))
            {
                var modules = await GetSubscribedFeaturesAsync(tenantId);
                modules.Add(FeatureConstants.SAAS_CONFIGURATION);
                modules.Add(FeatureConstants.USER_MANAGEMENT);
                modules.Add("dashboard");
                return modules;
            }

            int? userId = null;
            if (!string.IsNullOrWhiteSpace(userIdOrName))
            {
                if (int.TryParse(userIdOrName, out var parsed)) userId = parsed;
                else
                {
                    userId = await _context.Users.IgnoreQueryFilters()
                        .Where(u => u.TenantId == tenantId && u.Username == userIdOrName)
                        .Select(u => (int?)u.Id).FirstOrDefaultAsync();
                }
            }

            var effective = await _entitlements.ComputeEffectivePermissionsFromTablesAsync(tenantId, userId, userRole);
            effective.Add("dashboard");
            return effective;
        }

        public async Task<FeatureAuthorizationResult> AuthorizeFeatureAsync(ClaimsPrincipal user, Guid tenantId, string featureCode)
        {
            if (user?.Identity?.IsAuthenticated != true)
                return FeatureAuthorizationResult.Unauthorized("Authentication is required to access this resource.");

            // SaaS configuration is a platform/super-user concern, not a module.
            if (string.Equals(featureCode, FeatureConstants.SAAS_CONFIGURATION, StringComparison.OrdinalIgnoreCase))
            {
                return IsSuperUser(user)
                    ? FeatureAuthorizationResult.Success()
                    : FeatureAuthorizationResult.Forbidden("Access denied: SaaS Configuration is restricted to the platform operator.");
            }

            if (IsSuperUser(user)) return FeatureAuthorizationResult.Success();

            // [RequireFeature("<module>")] → the canonical module code must be in
            // the tenant's active tenant_modules. No alias normalization.
            var modules = await GetSubscribedFeaturesAsync(tenantId);
            if (modules.Contains(featureCode))
                return FeatureAuthorizationResult.Success();

            return FeatureAuthorizationResult.Forbidden($"Access denied: organization is not subscribed to module '{featureCode}'.");
        }
    }
}
