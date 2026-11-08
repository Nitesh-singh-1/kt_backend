using System;
using System.Collections.Generic;
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

        // ---- Logo object-storage reference (base64 -> MinIO/S3) ----
        // The authoritative object key lives in CustomSettingsJson (untouched by the
        // config PUT); GeneralJson.logoUrl holds the short absolute serve URL.
        Task<string> SetLogoAsync(Guid tenantId, string objectKey, string contentType, string logoUrl);
        Task<(string ObjectKey, string ContentType)?> GetLogoReferenceAsync(Guid tenantId);
        Task<string?> ClearLogoAsync(Guid tenantId);
        Task<List<(Guid TenantId, string DataUri)>> GetTenantsWithBase64LogosAsync();
    }
}
