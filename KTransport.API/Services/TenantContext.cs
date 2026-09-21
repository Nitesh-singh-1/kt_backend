using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace KTransport.API.Services
{
    public class TenantContext : ITenantContext
    {
        public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private readonly IHttpContextAccessor _httpContextAccessor;
        private Guid? _explicitTenantId;

        public TenantContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid CurrentTenantId
        {
            get
            {
                if (_explicitTenantId.HasValue && _explicitTenantId.Value != Guid.Empty)
                {
                    return _explicitTenantId.Value;
                }

                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null)
                {
                    // 1. Try to get tenant_id from JWT claims
                    var tenantClaim = httpContext.User?.FindFirst("tenant_id")?.Value
                                      ?? httpContext.User?.FindFirst("TenantId")?.Value;

                    if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out var claimTenantId))
                    {
                        return claimTenantId;
                    }

                    // 2. Try to get X-Tenant-ID header
                    if (httpContext.Request.Headers.TryGetValue("X-Tenant-ID", out var headerTenant) 
                        && Guid.TryParse(headerTenant.ToString(), out var headerTenantId))
                    {
                        return headerTenantId;
                    }
                }

                return DefaultTenantId;
            }
        }

        public bool HasTenant => CurrentTenantId != Guid.Empty;

        public void SetTenantId(Guid tenantId)
        {
            _explicitTenantId = tenantId;
        }
    }
}
