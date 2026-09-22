using System;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface ITenantConfigurationService
    {
        Task<TenantConfigurationDto> GetTenantConfigurationAsync(Guid tenantId);
        Task<TenantConfigurationDto> UpdateTenantConfigurationAsync(Guid tenantId, UpdateTenantConfigurationDto dto);
        Task<PublicTenantConfigDto?> GetPublicTenantConfigAsync(string codeOrDomain);
        Task<TenantFeatureFlagsDto> GetFeatureFlagsAsync(Guid tenantId);
        Task<TenantSubscriptionDto> GetSubscriptionDetailsAsync(Guid tenantId);
        Task<TenantConfigurationDto> ResetToDefaultsAsync(Guid tenantId);
        Task<bool> HasFeatureAccessAsync(Guid tenantId, string featureKey);
    }
}
