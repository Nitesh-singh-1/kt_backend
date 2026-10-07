using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace KTransport.API.Authorization
{
    /// <summary>
    /// TASK-039: per-page permission guard.
    ///
    /// Sibling to <see cref="RequireFeatureAttribute"/>. Where <c>RequireFeature</c> checks
    /// the organization's MODULE subscription (e.g. BILLING), <c>RequirePermission</c>
    /// checks a GRANULAR per-page key (e.g. <c>billing.bill_book</c>) against the user's
    /// effective permission set — the same set NavigationService hands the frontend via
    /// <c>GET /api/navigation/permissions</c>. Returns 401 for an unauthenticated caller
    /// and 403 when the key is absent. Super users (holders of the <c>*</c> wildcard
    /// returned by <see cref="INavigationService.GetUserPermissionsAsync"/>) pass through.
    ///
    /// This is additive on top of a class-level <c>[RequireFeature(MODULE)]</c>: the
    /// module gate runs first, then the per-page gate narrows access to specific
    /// endpoints inside the module.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _permissionKey;

        public RequirePermissionAttribute(string permissionKey)
        {
            _permissionKey = permissionKey ?? string.Empty;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    success = false,
                    message = "Authentication is required to access this endpoint."
                });
                return;
            }

            if (string.IsNullOrWhiteSpace(_permissionKey))
            {
                // A misconfigured attribute must not silently allow access.
                context.Result = new ObjectResult(new
                {
                    success = false,
                    message = "Access forbidden: required permission key is not configured."
                })
                {
                    StatusCode = 403
                };
                return;
            }

            var tenantContext = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
            var navService = context.HttpContext.RequestServices.GetRequiredService<INavigationService>();

            var role = user.FindFirst(ClaimTypes.Role)?.Value
                       ?? user.FindFirst("role")?.Value
                       ?? string.Empty;

            // Resolve the user from the JWT identity claim first, then fall back to
            // name claim. NavigationService accepts either (it looks the id up in the
            // UserOverridesJson map, which is keyed on usernames OR numeric ids).
            var userIdOrName = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? user.FindFirst(ClaimTypes.Name)?.Value
                               ?? user.FindFirst("sub")?.Value
                               ?? string.Empty;

            var permissions = await navService.GetUserPermissionsAsync(tenantContext.CurrentTenantId, role, userIdOrName);

            // Super users carry the "*" wildcard from NavigationService.
            if (permissions.Contains("*", StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            // Match on the raw key or its normalized form so an entitlement list that
            // uses the canonical FeatureConstant (e.g. "BILL_BOOK") still satisfies a
            // dotted page key (e.g. "billing.bill_book"). Normalize is defensive — if
            // the key doesn't map to a canonical, Normalize returns the uppercase form.
            var canonical = FeatureConstants.Normalize(_permissionKey);
            bool hasKey = permissions.Any(p =>
                string.Equals(p, _permissionKey, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(canonical) &&
                 string.Equals(FeatureConstants.Normalize(p), canonical, StringComparison.OrdinalIgnoreCase)));

            if (!hasKey)
            {
                context.Result = new ObjectResult(new
                {
                    success = false,
                    message = $"Access denied: this action requires the '{_permissionKey}' permission.",
                    requiredPermission = _permissionKey
                })
                {
                    StatusCode = 403
                };
            }
        }
    }
}
