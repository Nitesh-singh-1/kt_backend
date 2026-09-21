using System;

namespace KTransport.API.Services
{
    public interface ITenantContext
    {
        Guid CurrentTenantId { get; }
        void SetTenantId(Guid tenantId);
        bool HasTenant { get; }
    }
}
