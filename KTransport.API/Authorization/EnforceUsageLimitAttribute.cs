using System;
using System.Threading.Tasks;
using KTransport.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace KTransport.API.Authorization
{
    /// <summary>
    /// Stage 3 usage gate (AUTHORIZATION.md): blocks a create when the tenant has
    /// exhausted its subscription plan quota for the resource. Returns
    /// <c>402 Payment Required</c> — distinct from a 403 permission denial — so the
    /// frontend can prompt an upgrade rather than an access request.
    ///
    /// Ordered to fire AFTER <see cref="RequirePermissionAttribute"/> (Order 0): a
    /// caller who lacks permission gets 403, not a quota hint. Enforcement policy
    /// (grandfathering, unlimited sentinels) lives in <see cref="IUsageLimitService"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class EnforceUsageLimitAttribute : Attribute, IAsyncAuthorizationFilter, IOrderedFilter
    {
        private readonly UsageResource _resource;

        public int Order => 100; // after permission (0) and feature gates

        public EnforceUsageLimitAttribute(UsageResource resource)
        {
            _resource = resource;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var tenantContext = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
            var usage = context.HttpContext.RequestServices.GetRequiredService<IUsageLimitService>();

            var result = await usage.CheckAsync(tenantContext.CurrentTenantId, _resource);
            if (result.Allowed)
                return;

            context.Result = new ObjectResult(new
            {
                success = false,
                code = "USAGE_LIMIT_EXCEEDED",
                message = $"Your '{result.PlanName}' plan allows up to {result.Limit} " +
                          $"{_resource.ToString().ToLowerInvariant()} and you are at {result.Current}. " +
                          "Upgrade your plan to add more.",
                resource = _resource.ToString(),
                limit = result.Limit,
                current = result.Current,
                planName = result.PlanName
            })
            {
                StatusCode = StatusCodes.Status402PaymentRequired
            };
        }
    }
}
