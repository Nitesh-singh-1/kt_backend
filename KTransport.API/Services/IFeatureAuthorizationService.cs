using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Common;

namespace KTransport.API.Services
{
    public interface IFeatureAuthorizationService
    {
        /// <summary>
        /// Validates whether the caller has effective access to the specified feature code.
        /// Effective Access = Organization Subscription ∩ User Permissions.
        /// </summary>
        Task<FeatureAuthorizationResult> AuthorizeFeatureAsync(ClaimsPrincipal user, Guid tenantId, string featureCode);

        /// <summary>
        /// Validates whether the caller is a Super User / Tenant Owner within their own organization.
        /// NOTE: this is tenant-scoped in effect — any tenant's admin passes this. It must NOT be used
        /// to gate cross-tenant / platform-wide operations; use <see cref="IsPlatformAdmin"/> for those.
        /// </summary>
        bool IsSuperUser(ClaimsPrincipal user);

        /// <summary>
        /// Validates whether the caller is the PLATFORM operator: a super user belonging to the
        /// default/platform tenant. Only a platform admin may manage other tenants (list all,
        /// suspend, change plans, etc.). An onboarded client's admin is never a platform admin.
        /// </summary>
        bool IsPlatformAdmin(ClaimsPrincipal user);

        /// <summary>
        /// Returns the set of all features subscribed to by the organization.
        /// </summary>
        Task<HashSet<string>> GetSubscribedFeaturesAsync(Guid tenantId);

        /// <summary>
        /// Returns the set of effective features for the specified user in the tenant.
        /// </summary>
        Task<HashSet<string>> GetEffectiveFeaturesForUserAsync(Guid tenantId, string userRole, string userIdOrName);
    }

    public class FeatureAuthorizationResult
    {
        public bool IsAuthorized { get; set; }
        public string? FailureReason { get; set; }
        public int StatusCode { get; set; } = 403;

        public static FeatureAuthorizationResult Success() => new() { IsAuthorized = true };
        public static FeatureAuthorizationResult Forbidden(string reason) => new() { IsAuthorized = false, FailureReason = reason, StatusCode = 403 };
        public static FeatureAuthorizationResult Unauthorized(string reason) => new() { IsAuthorized = false, FailureReason = reason, StatusCode = 401 };
    }
}
