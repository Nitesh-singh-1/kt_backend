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

        /// <summary>Lists the users of any tenant (platform-operator use — bypasses tenant filter).</summary>
        Task<IEnumerable<TenantUserDto>> GetTenantUsersAsync(Guid tenantId);

        /// <summary>
        /// Sets a user's role within a tenant (platform-operator use). Allowed roles are constrained
        /// to admin / standard user. Returns (success, message).
        /// </summary>
        Task<(bool Success, string Message)> SetTenantUserRoleAsync(Guid tenantId, int userId, string role);
    }
}
