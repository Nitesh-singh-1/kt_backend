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
using KTransport.API.Services.Email;
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
        private readonly IAuditLogService _auditLogService;
        private readonly Email.IEmailSender _emailSender;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public TenantService(
            ILogger<TenantService> logger,
            KTransportDbContext context,
            IOptions<JwtSettings> jwtSettings,
            IAuditLogService auditLogService,
            Email.IEmailSender emailSender)
        {
            _logger = logger;
            _context = context;
            _jwtSettings = jwtSettings.Value;
            _auditLogService = auditLogService;
            _emailSender = emailSender;
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

                // 2. Create the tenant's own admin user.
                // The role is FORCED server-side to the standard tenant-admin role and the caller-supplied
                // request.AdminRole is intentionally ignored: this endpoint is public self-signup, so honoring
                // an arbitrary role would let a signup pick a privileged role string. The onboarded admin is a
                // TENANT admin only — platform authority additionally requires membership in the default tenant
                // (see FeatureAuthorizationService.IsPlatformAdmin), which onboarded tenants never have.
                const string assignedRole = "admin";

                var adminUser = new User
                {
                    TenantId = tenant.Id,
                    Username = request.AdminUsername.Trim(),
                    Password = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
                    FullName = request.AdminFullName.Trim(),
                    Mobile = request.AdminMobile,
                    Email = string.IsNullOrWhiteSpace(request.AdminEmail) ? null : request.AdminEmail.Trim(),
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

                // 5. Send welcome email to the new admin (best effort — never blocks onboarding).
                if (!string.IsNullOrWhiteSpace(adminUser.Email))
                {
                    var (subject, html) = EmailTemplates.Welcome(tenant.Name, adminUser.FullName ?? adminUser.Username, adminUser.Username);
                    await _emailSender.SendAsync(adminUser.Email!, subject, html, tenant.Name);
                }

                // 6. Generate JWT Token for immediate login
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
                        Mobile = adminUser.Mobile,
                        Email = adminUser.Email
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

            await _auditLogService.LogAsync("TenantStatusChanged", tenantId: tenantId, entityType: "Tenant", entityId: tenantId.ToString(), details: $"Status set to {(isActive ? "Active" : "Inactive")}");

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

            await _auditLogService.LogAsync("PlanChanged", tenantId: tenantId, entityType: "Tenant", entityId: tenantId.ToString(), details: $"Plan changed to {planTier}");

            return true;
        }

        private static readonly string[] AdminRoleValues = { "admin", "superadmin", "tenantadmin", "super_user", "tenant_owner" };

        public async Task<IEnumerable<TenantUserDto>> GetTenantUsersAsync(Guid tenantId)
        {
            var users = await _context.Users
                .IgnoreQueryFilters()
                .Where(u => u.TenantId == tenantId)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return users.Select(u => new TenantUserDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName ?? string.Empty,
                Role = u.Role ?? "SUB_USER",
                Mobile = u.Mobile,
                Email = u.Email,
                IsActive = u.IsActive ?? true,
                IsAdmin = AdminRoleValues.Contains((u.Role ?? "").ToLowerInvariant())
            });
        }

        public async Task<(bool Success, string Message)> SetTenantUserRoleAsync(Guid tenantId, int userId, string role)
        {
            // Constrain to a safe, known set — never let an arbitrary/elevated role string be injected.
            var normalized = (role ?? "").Trim();
            var isAdmin = string.Equals(normalized, "admin", StringComparison.OrdinalIgnoreCase);
            var isStandard = string.Equals(normalized, "SUB_USER", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(normalized, "user", StringComparison.OrdinalIgnoreCase);

            if (!isAdmin && !isStandard)
            {
                return (false, "Role must be either 'admin' or 'SUB_USER'.");
            }

            var user = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId);

            if (user == null)
            {
                return (false, "User not found in this organization.");
            }

            var targetRole = isAdmin ? "admin" : "SUB_USER";
            var previous = user.Role ?? "SUB_USER";
            user.Role = targetRole;
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync("TenantUserRoleChanged", tenantId: tenantId, entityType: "User",
                entityId: userId.ToString(), details: $"Role changed from '{previous}' to '{targetRole}' by platform operator");

            return (true, $"{user.Username} is now {(isAdmin ? "an administrator" : "a standard user")} of this organization.");
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
