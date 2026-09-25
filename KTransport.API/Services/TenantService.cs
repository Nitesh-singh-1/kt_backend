using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KTransport.API.Services
{
    public class TenantService : ITenantService
    {
        private readonly ILogger<TenantService> _logger;
        private readonly KTransportDbContext _context;
        private readonly JwtSettings _jwtSettings;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public TenantService(
            ILogger<TenantService> logger, 
            KTransportDbContext context, 
            IOptions<JwtSettings> jwtSettings)
        {
            _logger = logger;
            _context = context;
            _jwtSettings = jwtSettings.Value;
        }

        public async Task<TenantOnboardingResponse> OnboardTenantAsync(TenantOnboardingRequest request)
        {
            try
            {
                _logger.LogInformation("Onboarding new tenant: {OrgName} ({OrgCode})", request.OrganizationName, request.OrganizationCode);

                var normalizedCode = request.OrganizationCode.Trim().ToUpperInvariant();

                // Check if tenant code already exists
                var existingTenant = await _context.Tenants
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.Code.ToUpper() == normalizedCode);

                if (existingTenant != null)
                {
                    return new TenantOnboardingResponse
                    {
                        Success = false,
                        Message = $"Organization code '{request.OrganizationCode}' is already registered."
                    };
                }

                // Check if admin username exists across tenants or within tenant
                var existingUser = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == request.AdminUsername.Trim().ToLower());

                if (existingUser != null)
                {
                    return new TenantOnboardingResponse
                    {
                        Success = false,
                        Message = $"Username '{request.AdminUsername}' is already taken."
                    };
                }

                // 1. Create Tenant
                var tenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = request.OrganizationName.Trim(),
                    Code = normalizedCode,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Tenants.Add(tenant);
                await _context.SaveChangesAsync();

                // 2. Create Admin User for this Tenant
                var assignedRole = string.IsNullOrWhiteSpace(request.AdminRole) ? "admin" : request.AdminRole.Trim();

                var adminUser = new User
                {
                    TenantId = tenant.Id,
                    Username = request.AdminUsername.Trim(),
                    Password = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
                    FullName = request.AdminFullName.Trim(),
                    Mobile = request.AdminMobile,
                    Role = assignedRole,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(adminUser);
                await _context.SaveChangesAsync();

                // 3. Create Subscription Plan linkage
                var planTier = string.IsNullOrWhiteSpace(request.PlanTier) ? "Starter" : request.PlanTier.Trim();
                var plan = await _context.SubscriptionPlans
                    .FirstOrDefaultAsync(p => p.Tier.ToLower() == planTier.ToLower() || p.Name.ToLower() == planTier.ToLower());

                if (plan == null)
                {
                    plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Tier == "Starter");
                }

                if (plan != null)
                {
                    var subscription = new TenantSubscription
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        SubscriptionPlanId = plan.Id,
                        Status = "Active",
                        StartedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddYears(1),
                        IsAutoRenew = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.TenantSubscriptions.Add(subscription);
                }

                // 4. Configure Menu & Report Entitlements
                var enabledMenuKeys = request.EnabledModules != null && request.EnabledModules.Count > 0
                    ? request.EnabledModules
                    : new List<string> { "dashboard", "gr", "gr.list", "gr.entry", "challan", "challan.list", "challan.entry", "system", "system.settings" };

                // Always ensure core essentials
                if (!enabledMenuKeys.Contains("dashboard")) enabledMenuKeys.Insert(0, "dashboard");
                if (!enabledMenuKeys.Contains("system")) enabledMenuKeys.Add("system");
                if (!enabledMenuKeys.Contains("system.settings")) enabledMenuKeys.Add("system.settings");

                var standardReports = GetCatalogReports();
                var selectedReportKeys = request.EnabledReportKeys != null 
                    ? new HashSet<string>(request.EnabledReportKeys, StringComparer.OrdinalIgnoreCase) 
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var rep in standardReports)
                {
                    rep.IsEnabled = selectedReportKeys.Contains(rep.ReportKey);
                }

                var entitlementsDto = new TenantMenuEntitlementsDto
                {
                    TenantId = tenant.Id,
                    EnabledMenuKeys = enabledMenuKeys,
                    Reports = standardReports
                };

                var entitlementsJson = JsonSerializer.Serialize(entitlementsDto, JsonOptions);

                var setting = new TenantSetting
                {
                    TenantId = tenant.Id,
                    GeneralJson = JsonSerializer.Serialize(new { companyName = tenant.Name, companyCode = tenant.Code }, JsonOptions),
                    BillingAndTaxJson = JsonSerializer.Serialize(new { enableGstBilling = true, defaultGstRate = 5.0m }, JsonOptions),
                    DocumentSequencesJson = JsonSerializer.Serialize(new { grPrefix = "GR-", grNextNumber = 1, challanPrefix = "CH-", challanNextNumber = 1 }, JsonOptions),
                    OperationalWorkflowsJson = "{}",
                    FeatureFlagsJson = "{}",
                    IntegrationsJson = "{}",
                    CustomSettingsJson = "{}",
                    MenuEntitlementsJson = entitlementsJson,
                    CreatedAt = DateTime.UtcNow
                };

                _context.TenantSettings.Add(setting);
                await _context.SaveChangesAsync();

                // 5. Generate JWT Token for immediate login
                var token = GenerateJwtToken(adminUser);

                return new TenantOnboardingResponse
                {
                    Success = true,
                    Message = "Tenant, subscription, and custom module pack provisioned successfully.",
                    TenantId = tenant.Id,
                    OrganizationName = tenant.Name,
                    OrganizationCode = tenant.Code,
                    PlanTier = plan?.Tier ?? planTier,
                    EnabledModules = enabledMenuKeys,
                    Token = token,
                    AdminUser = new UserDto
                    {
                        Id = adminUser.Id,
                        Username = adminUser.Username,
                        FullName = adminUser.FullName ?? string.Empty,
                        Role = adminUser.Role ?? "admin",
                        Mobile = adminUser.Mobile
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error onboarding tenant {OrgName}", request.OrganizationName);
                return new TenantOnboardingResponse
                {
                    Success = false,
                    Message = $"Error during tenant onboarding: {ex.Message}"
                };
            }
        }

        public async Task<Tenant?> GetTenantByIdAsync(Guid tenantId)
        {
            return await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId);
        }

        public async Task<IEnumerable<TenantAdminListItemDto>> GetAllTenantsWithDetailsAsync()
        {
            var tenants = await _context.Tenants
                .IgnoreQueryFilters()
                .OrderBy(t => t.Name)
                .ToListAsync();

            var tenantIds = tenants.Select(t => t.Id).ToList();

            var users = await _context.Users
                .IgnoreQueryFilters()
                .Where(u => tenantIds.Contains(u.TenantId))
                .ToListAsync();

            var subscriptions = await _context.TenantSubscriptions
                .IgnoreQueryFilters()
                .Include(s => s.SubscriptionPlan)
                .Where(s => tenantIds.Contains(s.TenantId))
                .ToListAsync();

            var settings = await _context.TenantSettings
                .IgnoreQueryFilters()
                .Where(s => tenantIds.Contains(s.TenantId))
                .ToListAsync();

            var results = new List<TenantAdminListItemDto>();

            foreach (var tenant in tenants)
            {
                var tenantUsers = users.Where(u => u.TenantId == tenant.Id).ToList();
                var primaryAdmin = tenantUsers.FirstOrDefault(u => string.Equals(u.Role, "admin", StringComparison.OrdinalIgnoreCase)) 
                                   ?? tenantUsers.FirstOrDefault();

                var sub = subscriptions.FirstOrDefault(s => s.TenantId == tenant.Id);
                var setting = settings.FirstOrDefault(s => s.TenantId == tenant.Id);

                var enabledKeys = new List<string>();
                var enabledReports = new List<string>();

                if (setting != null && !string.IsNullOrWhiteSpace(setting.MenuEntitlementsJson) && setting.MenuEntitlementsJson != "{}")
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<TenantMenuEntitlementsDto>(setting.MenuEntitlementsJson, JsonOptions);
                        if (parsed != null)
                        {
                            enabledKeys = parsed.EnabledMenuKeys ?? new List<string>();
                            enabledReports = parsed.Reports?.Where(r => r.IsEnabled).Select(r => r.ReportKey).ToList() ?? new List<string>();
                        }
                    }
                    catch
                    {
                        // Ignore parse errors
                    }
                }

                // If default superadmin tenant, provide default full set if empty
                if (enabledKeys.Count == 0 && tenant.Id == TenantConstants.DefaultTenantId)
                {
                    enabledKeys = new List<string> { "dashboard", "gr", "gr.list", "gr.entry", "challan", "challan.list", "challan.entry", "reports", "system", "system.settings" };
                    enabledReports = new List<string> { "booking_register", "tax_summary", "party_outstanding", "trip_profitability", "vendor_payables" };
                }

                results.Add(new TenantAdminListItemDto
                {
                    Id = tenant.Id,
                    Name = tenant.Name,
                    Code = tenant.Code,
                    IsActive = tenant.IsActive,
                    CreatedAt = tenant.CreatedAt,
                    AdminUsername = primaryAdmin?.Username,
                    AdminFullName = primaryAdmin?.FullName,
                    AdminMobile = primaryAdmin?.Mobile,
                    UserCount = tenantUsers.Count,
                    SubscriptionPlanTier = sub?.SubscriptionPlan?.Tier ?? "Starter",
                    SubscriptionStatus = sub?.Status ?? "Active",
                    MonthlyPrice = sub?.SubscriptionPlan?.MonthlyPrice ?? 0,
                    EnabledMenuKeys = enabledKeys,
                    EnabledReportKeys = enabledReports,
                    EnabledReportsCount = enabledReports.Count
                });
            }

            return results;
        }

        public async Task<bool> UpdateTenantStatusAsync(Guid tenantId, bool isActive)
        {
            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null) return false;

            tenant.IsActive = isActive;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateTenantSubscriptionPlanAsync(Guid tenantId, string planTier)
        {
            var plan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.Tier.ToLower() == planTier.ToLower() || p.Name.ToLower() == planTier.ToLower());

            if (plan == null) return false;

            var sub = await _context.TenantSubscriptions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (sub == null)
            {
                sub = new TenantSubscription
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SubscriptionPlanId = plan.Id,
                    Status = "Active",
                    StartedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddYears(1),
                    IsAutoRenew = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.TenantSubscriptions.Add(sub);
            }
            else
            {
                sub.SubscriptionPlanId = plan.Id;
                sub.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role ?? "admin"),
                new Claim("FullName", user.FullName ?? string.Empty),
                new Claim("tenant_id", user.TenantId.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private static List<ReportEntitlementItemDto> GetCatalogReports()
        {
            return new List<ReportEntitlementItemDto>
            {
                new()
                {
                    ReportKey = "booking_register",
                    Title = "Consignment Booking Register",
                    Description = "Comprehensive log of all booked goods receipts with weight, freight, and consignor breakdown",
                    Category = "Operational",
                    Path = "/reports?tab=booking_register",
                    IsEnabled = true
                },
                new()
                {
                    ReportKey = "tax_summary",
                    Title = "GST & Tax Summary Report",
                    Description = "Taxable amounts, CGST, SGST, IGST, and RCM breakdowns for monthly GST returns",
                    Category = "Financial",
                    Path = "/reports?tab=tax_summary",
                    IsEnabled = true
                },
                new()
                {
                    ReportKey = "party_outstanding",
                    Title = "Customer Outstanding Ledger",
                    Description = "Real-time receivables, billed vs settled balance, and customer aging summary",
                    Category = "Financial",
                    Path = "/reports?tab=party_outstanding",
                    IsEnabled = true
                },
                new()
                {
                    ReportKey = "trip_profitability",
                    Title = "Trip Profitability & P&L",
                    Description = "Trip revenue vs diesel, toll, driver advance, and vehicle expense margin analysis",
                    Category = "Operational",
                    Path = "/reports?tab=trip_profitability",
                    IsEnabled = true
                },
                new()
                {
                    ReportKey = "vendor_payables",
                    Title = "Vendor & Lorry Hire Payables",
                    Description = "Transporter / vehicle broker hiring contracts, paid advances, and pending dues",
                    Category = "Financial",
                    Path = "/reports?tab=vendor_payables",
                    IsEnabled = true
                }
            };
        }
    }
}
