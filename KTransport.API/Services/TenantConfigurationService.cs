using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class TenantConfigurationService : ITenantConfigurationService
    {
        private readonly KTransportDbContext _context;
        private readonly ITenantContext _tenantContext;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public TenantConfigurationService(KTransportDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        public async Task<TenantConfigurationDto> GetTenantConfigurationAsync(Guid tenantId)
        {
            var setting = await _context.TenantSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (setting == null)
            {
                setting = await CreateDefaultSettingsForTenantAsync(tenantId);
            }

            return MapToDto(setting);
        }

        public async Task<TenantConfigurationDto> UpdateTenantConfigurationAsync(Guid tenantId, UpdateTenantConfigurationDto dto)
        {
            var setting = await _context.TenantSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (setting == null)
            {
                setting = await CreateDefaultSettingsForTenantAsync(tenantId);
            }

            if (dto.General != null)
            {
                setting.GeneralJson = JsonSerializer.Serialize(dto.General, JsonOptions);
            }

            if (dto.BillingAndTax != null)
            {
                setting.BillingAndTaxJson = JsonSerializer.Serialize(dto.BillingAndTax, JsonOptions);
            }

            if (dto.DocumentSequences != null && dto.DocumentSequences.Count > 0)
            {
                setting.DocumentSequencesJson = JsonSerializer.Serialize(dto.DocumentSequences, JsonOptions);
                await SyncNumberingSequencesAsync(tenantId, dto.DocumentSequences);
            }

            if (dto.OperationalWorkflows != null)
            {
                setting.OperationalWorkflowsJson = JsonSerializer.Serialize(dto.OperationalWorkflows, JsonOptions);
            }

            if (dto.FeatureFlags != null)
            {
                setting.FeatureFlagsJson = JsonSerializer.Serialize(dto.FeatureFlags, JsonOptions);
            }

            if (dto.Integrations != null)
            {
                setting.IntegrationsJson = JsonSerializer.Serialize(dto.Integrations, JsonOptions);
            }

            setting.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return MapToDto(setting);
        }

        public async Task<PublicTenantConfigDto?> GetPublicTenantConfigAsync(string codeOrDomain)
        {
            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Code.ToLower() == codeOrDomain.ToLower() || t.Name.ToLower() == codeOrDomain.ToLower());

            if (tenant == null)
            {
                return null;
            }

            var setting = await _context.TenantSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenant.Id);

            var general = setting != null
                ? DeserializeOrDefault<GeneralSettingsDto>(setting.GeneralJson)
                : new GeneralSettingsDto { CompanyName = tenant.Name };

            return new PublicTenantConfigDto
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                TenantCode = tenant.Code,
                CompanyName = general.CompanyName,
                LogoUrl = general.LogoUrl,
                FaviconUrl = general.FaviconUrl,
                ThemeColor = general.ThemeColor,
                CurrencyCode = general.CurrencyCode,
                CurrencySymbol = general.CurrencySymbol
            };
        }

        public async Task<TenantFeatureFlagsDto> GetFeatureFlagsAsync(Guid tenantId)
        {
            var setting = await _context.TenantSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (setting == null)
            {
                return new TenantFeatureFlagsDto();
            }

            return DeserializeOrDefault<TenantFeatureFlagsDto>(setting.FeatureFlagsJson);
        }

        public async Task<TenantSubscriptionDto> GetSubscriptionDetailsAsync(Guid tenantId)
        {
            var subscription = await _context.TenantSubscriptions
                .IgnoreQueryFilters()
                .Include(s => s.SubscriptionPlan)
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            var vehicleCount = await _context.Vehicles.IgnoreQueryFilters().CountAsync(v => v.TenantId == tenantId);
            var userCount = await _context.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == tenantId);
            
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var monthlyShipments = await _context.Shipments.IgnoreQueryFilters().CountAsync(s => s.TenantId == tenantId && s.CreatedAt >= startOfMonth);

            if (subscription == null || subscription.SubscriptionPlan == null)
            {
                return new TenantSubscriptionDto
                {
                    PlanName = "Starter Tier",
                    PlanTier = "Starter",
                    Status = "Active",
                    ExpiresAt = DateTime.UtcNow.AddYears(1),
                    MaxVehicles = 10,
                    CurrentVehicles = vehicleCount,
                    MaxUsers = 3,
                    CurrentUsers = userCount,
                    MaxMonthlyShipments = 500,
                    CurrentMonthlyShipments = monthlyShipments
                };
            }

            return new TenantSubscriptionDto
            {
                PlanName = subscription.SubscriptionPlan.Name,
                PlanTier = subscription.SubscriptionPlan.Tier,
                Status = subscription.Status,
                ExpiresAt = subscription.ExpiresAt,
                MaxVehicles = subscription.SubscriptionPlan.MaxVehicles,
                CurrentVehicles = vehicleCount,
                MaxUsers = subscription.SubscriptionPlan.MaxUsers,
                CurrentUsers = userCount,
                MaxMonthlyShipments = subscription.SubscriptionPlan.MaxMonthlyShipments,
                CurrentMonthlyShipments = monthlyShipments
            };
        }

        public async Task<TenantConfigurationDto> ResetToDefaultsAsync(Guid tenantId)
        {
            var setting = await _context.TenantSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            var defaultGeneral = new GeneralSettingsDto();
            var defaultBilling = new BillingAndTaxSettingsDto();
            var defaultSequences = GetDefaultSequences();
            var defaultWorkflows = new OperationalWorkflowSettingsDto();
            var defaultFlags = new TenantFeatureFlagsDto();
            var defaultIntegrations = new IntegrationSettingsDto();

            if (setting == null)
            {
                setting = new TenantSetting
                {
                    TenantId = tenantId,
                    GeneralJson = JsonSerializer.Serialize(defaultGeneral, JsonOptions),
                    BillingAndTaxJson = JsonSerializer.Serialize(defaultBilling, JsonOptions),
                    DocumentSequencesJson = JsonSerializer.Serialize(defaultSequences, JsonOptions),
                    OperationalWorkflowsJson = JsonSerializer.Serialize(defaultWorkflows, JsonOptions),
                    FeatureFlagsJson = JsonSerializer.Serialize(defaultFlags, JsonOptions),
                    IntegrationsJson = JsonSerializer.Serialize(defaultIntegrations, JsonOptions),
                    MenuEntitlementsJson = "{}",
                    CreatedAt = DateTime.UtcNow
                };
                _context.TenantSettings.Add(setting);
            }
            else
            {
                setting.GeneralJson = JsonSerializer.Serialize(defaultGeneral, JsonOptions);
                setting.BillingAndTaxJson = JsonSerializer.Serialize(defaultBilling, JsonOptions);
                setting.DocumentSequencesJson = JsonSerializer.Serialize(defaultSequences, JsonOptions);
                setting.OperationalWorkflowsJson = JsonSerializer.Serialize(defaultWorkflows, JsonOptions);
                setting.FeatureFlagsJson = JsonSerializer.Serialize(defaultFlags, JsonOptions);
                setting.IntegrationsJson = JsonSerializer.Serialize(defaultIntegrations, JsonOptions);
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await SyncNumberingSequencesAsync(tenantId, defaultSequences);

            return MapToDto(setting);
        }

        public async Task<bool> HasFeatureAccessAsync(Guid tenantId, string featureKey)
        {
            var flags = await GetFeatureFlagsAsync(tenantId);
            var prop = typeof(TenantFeatureFlagsDto).GetProperty(featureKey, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(bool))
            {
                return (bool)(prop.GetValue(flags) ?? false);
            }
            return true;
        }

        private async Task<TenantSetting> CreateDefaultSettingsForTenantAsync(Guid tenantId)
        {
            // Idempotency check: check if already exists
            var existing = await _context.TenantSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (existing != null)
            {
                return existing;
            }

            var defaultGeneral = new GeneralSettingsDto();
            var defaultBilling = new BillingAndTaxSettingsDto();
            var defaultSequences = GetDefaultSequences();
            var defaultWorkflows = new OperationalWorkflowSettingsDto();
            var defaultFlags = new TenantFeatureFlagsDto();
            var defaultIntegrations = new IntegrationSettingsDto();

            var setting = new TenantSetting
            {
                TenantId = tenantId,
                GeneralJson = JsonSerializer.Serialize(defaultGeneral, JsonOptions),
                BillingAndTaxJson = JsonSerializer.Serialize(defaultBilling, JsonOptions),
                DocumentSequencesJson = JsonSerializer.Serialize(defaultSequences, JsonOptions),
                OperationalWorkflowsJson = JsonSerializer.Serialize(defaultWorkflows, JsonOptions),
                FeatureFlagsJson = JsonSerializer.Serialize(defaultFlags, JsonOptions),
                IntegrationsJson = JsonSerializer.Serialize(defaultIntegrations, JsonOptions),
                MenuEntitlementsJson = "{}",
                CreatedAt = DateTime.UtcNow
            };

            _context.TenantSettings.Add(setting);
            await _context.SaveChangesAsync();
            await SyncNumberingSequencesAsync(tenantId, defaultSequences);

            return setting;
        }

        private List<DocumentSequenceDto> GetDefaultSequences()
        {
            return new List<DocumentSequenceDto>
            {
                new DocumentSequenceDto { DocType = "Invoice", Prefix = "INV", Suffix = "", PaddingDigits = 5, NextNumber = 1001, ResetPeriod = "Yearly" },
                new DocumentSequenceDto { DocType = "GR", Prefix = "GR", Suffix = "", PaddingDigits = 5, NextNumber = 2001, ResetPeriod = "Yearly" },
                new DocumentSequenceDto { DocType = "Challan", Prefix = "CHL", Suffix = "", PaddingDigits = 5, NextNumber = 3001, ResetPeriod = "Yearly" },
                new DocumentSequenceDto { DocType = "Trip", Prefix = "TRP", Suffix = "", PaddingDigits = 5, NextNumber = 4001, ResetPeriod = "Yearly" },
                new DocumentSequenceDto { DocType = "Claim", Prefix = "CLM", Suffix = "", PaddingDigits = 5, NextNumber = 5001, ResetPeriod = "Yearly" }
            };
        }

        private async Task SyncNumberingSequencesAsync(Guid tenantId, List<DocumentSequenceDto> sequences)
        {
            try
            {
                var existingSequences = await _context.NumberingSequences
                    .IgnoreQueryFilters()
                    .Where(s => s.TenantId == tenantId)
                    .ToListAsync();

                foreach (var seq in sequences)
                {
                    var entityType = seq.DocType.ToUpperInvariant();
                    var existing = existingSequences.FirstOrDefault(s => string.Equals(s.EntityType, entityType, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        _context.NumberingSequences.Add(new NumberingSequence
                        {
                            TenantId = tenantId,
                            EntityType = entityType,
                            Prefix = seq.Prefix,
                            CurrentValue = seq.NextNumber,
                            Padding = seq.PaddingDigits,
                            Year = DateTime.UtcNow.Year,
                            FormatPattern = $"{seq.Prefix}-{{YEAR}}-{{SEQ}}"
                        });
                    }
                    else
                    {
                        existing.Prefix = seq.Prefix;
                        existing.Padding = seq.PaddingDigits;
                        if (seq.NextNumber > existing.CurrentValue)
                        {
                            existing.CurrentValue = seq.NextNumber;
                        }
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch
            {
                // Numbering sequence table sync fallback
            }
        }

        private static TenantConfigurationDto MapToDto(TenantSetting setting)
        {
            return new TenantConfigurationDto
            {
                TenantId = setting.TenantId,
                General = DeserializeOrDefault<GeneralSettingsDto>(setting.GeneralJson),
                BillingAndTax = DeserializeOrDefault<BillingAndTaxSettingsDto>(setting.BillingAndTaxJson),
                DocumentSequences = DeserializeOrDefault<List<DocumentSequenceDto>>(setting.DocumentSequencesJson),
                OperationalWorkflows = DeserializeOrDefault<OperationalWorkflowSettingsDto>(setting.OperationalWorkflowsJson),
                FeatureFlags = DeserializeOrDefault<TenantFeatureFlagsDto>(setting.FeatureFlagsJson),
                Integrations = DeserializeOrDefault<IntegrationSettingsDto>(setting.IntegrationsJson),
                UpdatedAt = setting.UpdatedAt
            };
        }

        private static T DeserializeOrDefault<T>(string json) where T : new()
        {
            if (string.IsNullOrWhiteSpace(json) || json == "{}")
            {
                return new T();
            }

            try
            {
                return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T();
            }
            catch
            {
                return new T();
            }
        }
    }
}
