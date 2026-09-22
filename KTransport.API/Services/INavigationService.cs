using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface INavigationService
    {
        Task<List<DynamicMenuItemDto>> GetDynamicMenuAsync(Guid tenantId, string userRole);
        Task<List<string>> GetUserPermissionsAsync(Guid tenantId, string userRole);
        Task<TenantMenuEntitlementsDto> GetTenantMenuEntitlementsAsync(Guid tenantId);
        Task<TenantMenuEntitlementsDto> UpdateTenantMenuEntitlementsAsync(Guid tenantId, TenantMenuEntitlementsDto dto);
    }
}
