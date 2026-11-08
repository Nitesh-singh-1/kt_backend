using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using KTransport.API.Services.Email;
using KTransport.API.Services.Rbac;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
                var normalizedCode = request.OrganizationCode.Trim().ToUpperInvariant();

                if (await _context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Code.ToUpper() == normalizedCode))
                    return new TenantOnboardingResponse { Success = false, Message = $"Organization code '{request.OrganizationCode}' is already registered." };

                if (await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Username.ToLower() == request.AdminUsername.Trim().ToLower()))
                    return new TenantOnboardingResponse { Success = false, Message = $"Username '{request.AdminUsername}' is already taken." };

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

                // Public self-signup ALWAYS forces the tenant-admin role.
                const string assignedRole = "admin";
                var adminUser = new User
                {
                    TenantId = tenant.Id,
                    Username = request.AdminUsername.Trim(),
                    Password = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
                    FullName = request.AdminFullName.Trim(),
                    Mobile = request.AdminMobile,
                    Email = string.IsNullOrWhiteSpace(request.AdminEmail) ? null : request.AdminEmail.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Users.Add(adminUser);
                await _context.SaveChangesAsync();

                // Self-signup defaults to the Professional plan ceiling (contract §D).
                var planTier = string.IsNullOrWhiteSpace(request.PlanTier) ? "Professional" : request.PlanTier.Trim();
                await LinkBillingPlanAsync(tenant.Id, planTier);
                await CreateTenantSettingsAsync(tenant);
                await SeedReportEntitlementsAsync(tenant.Id, request.EnabledReportKeys, adminUser.Id);

                await TenantProvisioning.ProvisionTenantAsync(
                    _context, tenant.Id, adminUser.Id, assignedRole, planTier,
                    explicitModuleCodes: request.EnabledModules, actingUserId: adminUser.Id);

                if (!string.IsNullOrWhiteSpace(adminUser.Email))
                {
                    var (subject, html) = EmailTemplates.Welcome(tenant.Name, adminUser.FullName ?? adminUser.Username, adminUser.Username);
                    await _emailSender.SendAsync(adminUser.Email!, subject, html, tenant.Name);
                }

                var token = await IssueTokenAsync(adminUser);
                var moduleCodes = await ActiveModuleCodesAsync(tenant.Id);

                return new TenantOnboardingResponse
                {
                    Success = true,
                    Message = "Tenant provisioned successfully.",
                    TenantId = tenant.Id,
                    OrganizationName = tenant.Name,
                    OrganizationCode = tenant.Code,
                    PlanTier = planTier,
                    EnabledModules = moduleCodes,
                    Token = token,
                    AdminUser = new UserDto
                    {
                        Id = adminUser.Id,
                        Username = adminUser.Username,
                        FullName = adminUser.FullName ?? string.Empty,
                        Role = assignedRole,
                        Mobile = adminUser.Mobile,
                        Email = adminUser.Email
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error onboarding tenant {OrgName}", request.OrganizationName);
                return new TenantOnboardingResponse { Success = false, Message = $"Error during tenant onboarding: {ex.Message}" };
            }
        }

        public async Task<TenantOnboardingResponse> OnboardTenantByAdminAsync(TenantAdminOnboardRequest request, int? platformAdminUserId)
        {
            try
            {
                var normalizedCode = request.OrganizationCode.Trim().ToUpperInvariant();

                if (await _context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Code.ToUpper() == normalizedCode))
                    return new TenantOnboardingResponse { Success = false, Message = $"Organization code '{request.OrganizationCode}' is already registered." };

                if (await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Username.ToLower() == request.AdminUsername.Trim().ToLower()))
                    return new TenantOnboardingResponse { Success = false, Message = $"Username '{request.AdminUsername}' is already taken." };

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

                var roleCode = string.IsNullOrWhiteSpace(request.AdminRoleCode) ? "admin" : request.AdminRoleCode.Trim();
                var adminUser = new User
                {
                    TenantId = tenant.Id,
                    Username = request.AdminUsername.Trim(),
                    Password = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
                    FullName = request.AdminFullName.Trim(),
                    Mobile = request.AdminMobile,
                    Email = string.IsNullOrWhiteSpace(request.AdminEmail) ? null : request.AdminEmail.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Users.Add(adminUser);
                await _context.SaveChangesAsync();

                var planTier = string.IsNullOrWhiteSpace(request.PlanTier) ? "Professional" : request.PlanTier.Trim();
                await LinkBillingPlanAsync(tenant.Id, planTier);
                await CreateTenantSettingsAsync(tenant);
                await SeedReportEntitlementsAsync(tenant.Id, request.EnabledReportKeys, platformAdminUserId ?? adminUser.Id);

                var explicitCodes = (request.EnabledModuleCodes != null && request.EnabledModuleCodes.Count > 0)
                    ? request.EnabledModuleCodes : null;
                await TenantProvisioning.ProvisionTenantAsync(
                    _context, tenant.Id, adminUser.Id, roleCode, planTier,
                    explicitModuleCodes: explicitCodes, actingUserId: platformAdminUserId ?? adminUser.Id);

                var moduleCodes = await ActiveModuleCodesAsync(tenant.Id);
                await _auditLogService.LogAsync("TenantCreatedByPlatformAdmin", tenantId: tenant.Id,
                    entityType: "Tenant", entityId: tenant.Id.ToString(),
                    details: $"Platform admin {platformAdminUserId} onboarded tenant {tenant.Code} with admin role '{roleCode}' and modules [{string.Join(",", moduleCodes)}]");

                var token = await IssueTokenAsync(adminUser);

                return new TenantOnboardingResponse
                {
                    Success = true,
                    Message = "Tenant onboarded by platform admin.",
                    TenantId = tenant.Id,
                    OrganizationName = tenant.Name,
                    OrganizationCode = tenant.Code,
                    PlanTier = planTier,
                    EnabledModules = moduleCodes,
                    Token = token,
                    AdminUser = new UserDto
                    {
                        Id = adminUser.Id,
                        Username = adminUser.Username,
                        FullName = adminUser.FullName ?? string.Empty,
                        Role = roleCode,
                        Mobile = adminUser.Mobile,
                        Email = adminUser.Email
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnboardTenantByAdmin failed for {Org}", request.OrganizationName);
                return new TenantOnboardingResponse { Success = false, Message = $"Error during admin onboarding: {ex.Message}" };
            }
        }

        public async Task<Tenant?> GetTenantByIdAsync(Guid tenantId)
            => await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId);

        public async Task<IEnumerable<TenantAdminListItemDto>> GetAllTenantsWithDetailsAsync()
        {
            var tenants = await _context.Tenants.IgnoreQueryFilters().OrderBy(t => t.Name).ToListAsync();
            var tenantIds = tenants.Select(t => t.Id).ToList();

            var users = await _context.Users.IgnoreQueryFilters().Where(u => tenantIds.Contains(u.TenantId)).ToListAsync();
            var subscriptions = await _context.TenantSubscriptions.IgnoreQueryFilters()
                .Include(s => s.SubscriptionPlan).Where(s => tenantIds.Contains(s.TenantId)).ToListAsync();

            var moduleRows = await (
                from tm in _context.TenantModules.IgnoreQueryFilters()
                join m in _context.Modules.IgnoreQueryFilters() on tm.ModuleId equals m.Id
                where tenantIds.Contains(tm.TenantId) && tm.EnabledUntil == null
                select new { tm.TenantId, m.Code }).ToListAsync();
            var modulesByTenant = moduleRows.GroupBy(x => x.TenantId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Code).OrderBy(c => c).ToList());

            var repRows = await _context.TenantReportEntitlements.IgnoreQueryFilters()
                .Where(r => tenantIds.Contains(r.TenantId) && r.IsEnabled).ToListAsync();

            // Admin detection via user_roles → roles.code ∈ admin set.
            var adminUserIds = (await (
                from ur in _context.UserRoles.IgnoreQueryFilters()
                join r in _context.Roles.IgnoreQueryFilters() on ur.RoleId equals r.Id
                where tenantIds.Contains(ur.TenantId) && ur.RevokedAt == null && r.IsActive
                      && RbacRoles.AdminRoleCodeList.Contains(r.Code.ToLower())
                select ur.UserId).ToListAsync()).ToHashSet();

            var results = new List<TenantAdminListItemDto>();
            foreach (var tenant in tenants)
            {
                var tenantUsers = users.Where(u => u.TenantId == tenant.Id).ToList();
                var primaryAdmin = tenantUsers.FirstOrDefault(u => adminUserIds.Contains(u.Id)) ?? tenantUsers.FirstOrDefault();
                var sub = subscriptions.FirstOrDefault(s => s.TenantId == tenant.Id);
                var enabledKeys = modulesByTenant.TryGetValue(tenant.Id, out var mc) ? mc : new List<string>();
                var enabledReports = repRows.Where(r => r.TenantId == tenant.Id).Select(r => r.ReportKey).ToList();

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
            var tenant = await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant == null) return false;
            tenant.IsActive = isActive;
            await _context.SaveChangesAsync();
            await _auditLogService.LogAsync("TenantStatusChanged", tenantId: tenantId, entityType: "Tenant", entityId: tenantId.ToString(), details: $"Status set to {(isActive ? "Active" : "Inactive")}");
            return true;
        }

        public async Task<bool> UpdateTenantSubscriptionPlanAsync(Guid tenantId, string planTier)
        {
            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Tier.ToLower() == planTier.ToLower() || p.Name.ToLower() == planTier.ToLower());
            if (plan == null) return false;

            var sub = await _context.TenantSubscriptions.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.TenantId == tenantId);
            if (sub == null)
            {
                _context.TenantSubscriptions.Add(new TenantSubscription
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, SubscriptionPlanId = plan.Id,
                    Status = "Active", StartedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddYears(1), IsAutoRenew = true, CreatedAt = DateTime.UtcNow
                });
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

        public async Task<IEnumerable<TenantUserDto>> GetTenantUsersAsync(Guid tenantId)
        {
            var users = await _context.Users.IgnoreQueryFilters()
                .Where(u => u.TenantId == tenantId).OrderBy(u => u.FullName).ToListAsync();

            var rolesByUser = (await (
                from ur in _context.UserRoles.IgnoreQueryFilters()
                join r in _context.Roles.IgnoreQueryFilters() on ur.RoleId equals r.Id
                where ur.TenantId == tenantId && ur.RevokedAt == null && r.IsActive
                select new { ur.UserId, r.Code }).ToListAsync())
                .GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.Select(x => x.Code).ToList());

            return users.Select(u =>
            {
                var codes = rolesByUser.TryGetValue(u.Id, out var c) ? c : new List<string>();
                var isAdmin = codes.Any(x => RbacRoles.IsAdminRole(x));
                var primary = codes.FirstOrDefault(x => RbacRoles.IsAdminRole(x)) ?? codes.FirstOrDefault() ?? "SUB_USER";
                return new TenantUserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    FullName = u.FullName ?? string.Empty,
                    Role = primary,
                    Mobile = u.Mobile,
                    Email = u.Email,
                    IsActive = u.IsActive ?? true,
                    IsAdmin = isAdmin
                };
            });
        }

        public async Task<(bool Success, string Message)> SetTenantUserRoleAsync(Guid tenantId, int userId, string role)
        {
            var normalized = (role ?? "").Trim();
            var isAdmin = string.Equals(normalized, "admin", StringComparison.OrdinalIgnoreCase);
            var isStandard = string.Equals(normalized, "SUB_USER", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(normalized, "user", StringComparison.OrdinalIgnoreCase);
            if (!isAdmin && !isStandard)
                return (false, "Role must be either 'admin' or 'SUB_USER'.");

            var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId);
            if (user == null) return (false, "User not found in this organization.");

            var adminRole = await _context.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Code.ToLower() == "admin");
            if (isAdmin)
            {
                if (adminRole == null) { await TenantProvisioning.EnsureSystemRolesAsync(_context, tenantId, null); adminRole = await _context.Roles.IgnoreQueryFilters().FirstAsync(r => r.TenantId == tenantId && r.Code.ToLower() == "admin"); }
                await TenantProvisioning.AssignUserRoleAsync(_context, tenantId, userId, "admin", null);
            }
            else
            {
                // Standard user: revoke any active admin assignment.
                if (adminRole != null)
                {
                    var active = await _context.UserRoles.IgnoreQueryFilters()
                        .Where(ur => ur.TenantId == tenantId && ur.UserId == userId && ur.RoleId == adminRole.Id && ur.RevokedAt == null)
                        .ToListAsync();
                    foreach (var ur in active) { ur.RevokedAt = DateTime.UtcNow; ur.RevokeReason = "role changed to standard by platform operator"; }
                    await _context.SaveChangesAsync();
                }
            }

            await _auditLogService.LogAsync("TenantUserRoleChanged", tenantId: tenantId, entityType: "User",
                entityId: userId.ToString(), details: $"Role set to '{(isAdmin ? "admin" : "SUB_USER")}' by platform operator");
            return (true, $"{user.Username} is now {(isAdmin ? "an administrator" : "a standard user")} of this organization.");
        }

        public IReadOnlyList<SystemRoleTemplateDto> GetSystemRoleTemplates()
            => new[]
            {
                new SystemRoleTemplateDto { Code = "admin", Name = "Administrator", Description = "Full tenant management + all enabled modules" },
                new SystemRoleTemplateDto { Code = "operations_manager", Name = "Operations Manager", Description = "Operational modules + reports" },
                new SystemRoleTemplateDto { Code = "supervisor", Name = "Supervisor", Description = "Approve / edit operational docs" },
                new SystemRoleTemplateDto { Code = "operator", Name = "Operator", Description = "Create own documents only" },
                new SystemRoleTemplateDto { Code = "billing_executive", Name = "Billing Executive", Description = "Billing + invoices CRUD" },
                new SystemRoleTemplateDto { Code = "accountant", Name = "Accountant", Description = "Financial reports + view-only operations" },
                new SystemRoleTemplateDto { Code = "viewer", Name = "Viewer", Description = "View-only everything the tenant has" },
            };

        public async Task ReconcileSystemRolePermissionsForTenantAsync(Guid tenantId, int? actingUserId)
            => await TenantProvisioning.SeedRolePermissionsAsync(_context, tenantId, actingUserId);

        // ---------------- helpers ----------------

        private async Task<string> IssueTokenAsync(User adminUser)
        {
            var (isSuper, roleCode) = await RbacTokenHelper.ResolvePrincipalAsync(_context, adminUser.TenantId, adminUser.Id);
            adminUser.Role = roleCode;
            return RbacTokenHelper.GenerateToken(adminUser, isSuper, roleCode, _jwtSettings);
        }

        private async Task<List<string>> ActiveModuleCodesAsync(Guid tenantId)
            => await (from tm in _context.TenantModules.IgnoreQueryFilters()
                      join m in _context.Modules.IgnoreQueryFilters() on tm.ModuleId equals m.Id
                      where tm.TenantId == tenantId && tm.EnabledUntil == null
                      orderby m.Code
                      select m.Code).ToListAsync();

        private async Task LinkBillingPlanAsync(Guid tenantId, string planTier)
        {
            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Tier.ToLower() == planTier.ToLower() || p.Name.ToLower() == planTier.ToLower())
                       ?? await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Tier == "Professional")
                       ?? await _context.SubscriptionPlans.FirstOrDefaultAsync();
            if (plan == null) return;
            _context.TenantSubscriptions.Add(new TenantSubscription
            {
                Id = Guid.NewGuid(), TenantId = tenantId, SubscriptionPlanId = plan.Id,
                Status = "Active", StartedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddYears(1), IsAutoRenew = true, CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        private async Task CreateTenantSettingsAsync(Tenant tenant)
        {
            if (await _context.TenantSettings.IgnoreQueryFilters().AnyAsync(s => s.TenantId == tenant.Id)) return;
            _context.TenantSettings.Add(new TenantSetting
            {
                TenantId = tenant.Id,
                GeneralJson = JsonSerializer.Serialize(new { companyName = tenant.Name, companyCode = tenant.Code }, JsonOptions),
                BillingAndTaxJson = JsonSerializer.Serialize(new { enableGstBilling = true, defaultGstRate = 5.0m }, JsonOptions),
                DocumentSequencesJson = JsonSerializer.Serialize(new { grPrefix = "GR-", grNextNumber = 1, challanPrefix = "CH-", challanNextNumber = 1 }, JsonOptions),
                OperationalWorkflowsJson = "{}",
                FeatureFlagsJson = "{}",
                IntegrationsJson = "{}",
                CustomSettingsJson = "{}",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        private async Task SeedReportEntitlementsAsync(Guid tenantId, List<string>? enabledReportKeys, int? actingUserId)
        {
            var selected = enabledReportKeys != null
                ? new HashSet<string>(enabledReportKeys, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rep in GetCatalogReports())
            {
                if (await _context.TenantReportEntitlements.IgnoreQueryFilters().AnyAsync(r => r.TenantId == tenantId && r.ReportKey == rep.ReportKey)) continue;
                _context.TenantReportEntitlements.Add(new TenantReportEntitlement
                {
                    TenantId = tenantId,
                    ReportKey = rep.ReportKey,
                    IsEnabled = selected.Contains(rep.ReportKey),
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = actingUserId
                });
            }
            await _context.SaveChangesAsync();
        }

        private static List<ReportEntitlementItemDto> GetCatalogReports() => new()
        {
            new() { ReportKey = "booking_register", Title = "Consignment Booking Register", Category = "Operational", Path = "/reports?tab=booking_register", IsEnabled = true },
            new() { ReportKey = "tax_summary", Title = "GST & Tax Summary Report", Category = "Financial", Path = "/reports?tab=tax_summary", IsEnabled = true },
            new() { ReportKey = "party_outstanding", Title = "Customer Outstanding Ledger", Category = "Financial", Path = "/reports?tab=party_outstanding", IsEnabled = true },
            new() { ReportKey = "trip_profitability", Title = "Trip Profitability & P&L", Category = "Operational", Path = "/reports?tab=trip_profitability", IsEnabled = true },
            new() { ReportKey = "vendor_payables", Title = "Vendor & Lorry Hire Payables", Category = "Financial", Path = "/reports?tab=vendor_payables", IsEnabled = true }
        };

        // ---------- tenant usage snapshot (unchanged) ----------
        private const int CriticalPercent = 80;

        public async Task<TenantUsageDto> GetUsageSnapshotAsync(Guid tenantId)
        {
            var subscription = await _context.TenantSubscriptions.IgnoreQueryFilters()
                .Include(s => s.SubscriptionPlan).FirstOrDefaultAsync(s => s.TenantId == tenantId);

            var vehicleCount = await _context.Vehicles.IgnoreQueryFilters().CountAsync(v => v.TenantId == tenantId);
            var userCount = await _context.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == tenantId);
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthlyShipmentCount = await _context.Shipments.IgnoreQueryFilters().CountAsync(s => s.TenantId == tenantId && s.CreatedAt >= startOfMonth);

            var planTier = subscription?.SubscriptionPlan?.Tier ?? "Starter";
            var planStatus = subscription?.Status ?? "Active";
            var maxVehicles = subscription?.SubscriptionPlan?.MaxVehicles ?? 10;
            var maxUsers = subscription?.SubscriptionPlan?.MaxUsers ?? 3;
            var maxMonthlyShipments = subscription?.SubscriptionPlan?.MaxMonthlyShipments ?? 500;

            var vehicles = BuildResource(vehicleCount, maxVehicles);
            var usersR = BuildResource(userCount, maxUsers);
            var shipments = BuildResource(monthlyShipmentCount, maxMonthlyShipments);

            var warnings = new List<string>();
            if (vehicles.IsCritical) warnings.Add($"Fleet quota near limit — {vehicles.Current} of {vehicles.Max} vehicles used ({vehicles.Percent}%).");
            if (usersR.IsCritical) warnings.Add($"User seat quota near limit — {usersR.Current} of {usersR.Max} seats used ({usersR.Percent}%).");
            if (shipments.IsCritical) warnings.Add($"Monthly shipment quota near limit — {shipments.Current} of {shipments.Max} shipments this month ({shipments.Percent}%).");

            return new TenantUsageDto
            {
                PlanTier = planTier,
                PlanStatus = planStatus,
                ExpiresAt = subscription?.ExpiresAt,
                Vehicles = vehicles,
                Users = usersR,
                MonthlyShipments = shipments,
                Warnings = warnings,
            };
        }

        private static ResourceUsageDto BuildResource(int current, int max)
        {
            int percent = max <= 0 ? 0 : (int)Math.Min(100, Math.Round((decimal)current * 100 / max));
            return new ResourceUsageDto { Current = current, Max = max, Percent = percent, IsCritical = percent >= CriticalPercent };
        }
    }
}
