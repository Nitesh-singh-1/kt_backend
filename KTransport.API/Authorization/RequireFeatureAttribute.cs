using System;
using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace KTransport.API.Authorization
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class RequireFeatureAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string[] _featureCodes;

        public RequireFeatureAttribute(string featureCode)
        {
            _featureCodes = new[] { featureCode };
        }

        public RequireFeatureAttribute(params string[] featureCodes)
        {
            _featureCodes = featureCodes ?? Array.Empty<string>();
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

            var tenantContext = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
            var authService = context.HttpContext.RequestServices.GetRequiredService<IFeatureAuthorizationService>();

            var tenantId = tenantContext.CurrentTenantId;
            if (_featureCodes.Length == 0) return;

            bool anyAuthorized = false;
            FeatureAuthorizationResult? lastResult = null;

            foreach (var fc in _featureCodes)
            {
                var result = await authService.AuthorizeFeatureAsync(user, tenantId, fc);
                if (result.IsAuthorized)
                {
                    anyAuthorized = true;
                    break;
                }
                lastResult = result;
            }

            if (!anyAuthorized)
            {
                context.Result = new ObjectResult(new
                {
                    success = false,
                    message = lastResult?.FailureReason ?? "Access forbidden.",
                    requiredFeature = string.Join(", ", _featureCodes)
                })
                {
                    StatusCode = lastResult?.StatusCode ?? 403
                };
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class RequireSuperUserAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    success = false,
                    message = "Authentication is required to access this endpoint."
                });
                return Task.CompletedTask;
            }

            var authService = context.HttpContext.RequestServices.GetRequiredService<IFeatureAuthorizationService>();
            if (!authService.IsSuperUser(user))
            {
                context.Result = new ObjectResult(new
                {
                    success = false,
                    message = "Access denied: This configuration area is strictly restricted to Organization Super Users."
                })
                {
                    StatusCode = 403
                };
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Restricts an endpoint to the PLATFORM operator (a super user of the default/platform tenant).
    /// Use this for cross-tenant / platform-wide management — an onboarded client's admin must never pass.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class RequirePlatformAdminAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    success = false,
                    message = "Authentication is required to access this endpoint."
                });
                return Task.CompletedTask;
            }

            var authService = context.HttpContext.RequestServices.GetRequiredService<IFeatureAuthorizationService>();
            if (!authService.IsPlatformAdmin(user))
            {
                context.Result = new ObjectResult(new
                {
                    success = false,
                    message = "Access denied: platform administration is restricted to the platform operator."
                })
                {
                    StatusCode = 403
                };
            }

            return Task.CompletedTask;
        }
    }
}
