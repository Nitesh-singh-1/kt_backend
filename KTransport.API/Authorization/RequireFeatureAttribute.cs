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
        private readonly string _featureCode;

        public RequireFeatureAttribute(string featureCode)
        {
            _featureCode = featureCode;
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
            var result = await authService.AuthorizeFeatureAsync(user, tenantId, _featureCode);

            if (!result.IsAuthorized)
            {
                context.Result = new ObjectResult(new
                {
                    success = false,
                    message = result.FailureReason ?? "Access forbidden.",
                    requiredFeature = _featureCode
                })
                {
                    StatusCode = result.StatusCode
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
}
