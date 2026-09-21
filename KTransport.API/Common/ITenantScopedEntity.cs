using System;

namespace KTransport.API.Common
{
    public static class TenantConstants
    {
        public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    }

    public interface ITenantScopedEntity
    {
        Guid TenantId { get; set; }
    }
}
