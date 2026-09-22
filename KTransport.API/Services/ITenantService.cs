using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface ITenantService
    {
        Task<TenantOnboardingResponse> OnboardTenantAsync(TenantOnboardingRequest request);
        Task<Tenant?> GetTenantByIdAsync(Guid tenantId);
        Task<IEnumerable<TenantAdminListItemDto>> GetAllTenantsWithDetailsAsync();
        Task<bool> UpdateTenantStatusAsync(Guid tenantId, bool isActive);
        Task<bool> UpdateTenantSubscriptionPlanAsync(Guid tenantId, string planTier);
    }
}
