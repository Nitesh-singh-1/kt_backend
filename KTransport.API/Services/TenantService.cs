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
                    // TASK-044 Phase 3: menu_entitlements_json dropped — entitlements go to normalized tables below.
                    CreatedAt = DateTime.UtcNow
                };

                _context.TenantSettings.Add(setting);

                // TASK-044 Phase 3: persist tenant subscription + enabled reports into the normalized tables.
                _context.TenantEntitlementSubscriptions.Add(new TenantEntitlementSubscription
                {
                    TenantId = tenant.Id,
                    PlanTier = string.IsNullOrWhiteSpace(planTier) ? "Enterprise" : planTier,
                    EnabledFeatureKeys = enabledMenuKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    EffectiveFrom = DateTime.UtcNow,
                    EffectiveUntil = null,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = adminUser.Id
                });

                foreach (var rep in standardReports)
                {
                    _context.TenantReportEntitlements.Add(new TenantReportEntitlement
                    {
                        TenantId = tenant.Id,
                        ReportKey = rep.ReportKey,
                        IsEnabled = rep.IsEnabled,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = adminUser.Id
                    });
                }

                await _context.SaveChangesAsync();

                // TASK-046 Phase 1: seed the 7 system roles for the new tenant
                // and insert a user_roles M2M row for the admin user. Also seed
                // tenant_modules rows from the enabledMenuKeys so the module
                // intersection in EntitlementsService works out of the box.
                await SeedSystemRolesForTenantAsync(tenant.Id, enabledMenuKeys, adminUser.Id);
                await SeedTenantModulesFromFeatureKeysAsync(tenant.Id, enabledMenuKeys, adminUser.Id);
                await AssignUserRoleAsync(tenant.Id, adminUser.Id, assignedRole, assignedBy: adminUser.Id);
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

            // TASK-044 Phase 3: enabled modules/reports now live in normalized tables.
            var entSubs = await _context.TenantEntitlementSubscriptions
                .IgnoreQueryFilters()
                .Where(s => tenantIds.Contains(s.TenantId) && s.EffectiveUntil == null)
                .ToListAsync();
            var repRows = await _context.TenantReportEntitlements
                .IgnoreQueryFilters()
                .Where(r => tenantIds.Contains(r.TenantId) && r.IsEnabled)
                .ToListAsync();

            var results = new List<TenantAdminListItemDto>();

            foreach (var tenant in tenants)
            {
                var tenantUsers = users.Where(u => u.TenantId == tenant.Id).ToList();
                var primaryAdmin = tenantUsers.FirstOrDefault(u => string.Equals(u.Role, "admin", StringComparison.OrdinalIgnoreCase)) 
                                   ?? tenantUsers.FirstOrDefault();

                var sub = subscriptions.FirstOrDefault(s => s.TenantId == tenant.Id);

                var entSub = entSubs.FirstOrDefault(s => s.TenantId == tenant.Id);
                var enabledKeys = entSub?.EnabledFeatureKeys?.ToList() ?? new List<string>();
                var enabledReports = repRows.Where(r => r.TenantId == tenant.Id).Select(r => r.ReportKey).ToList();

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

        // ---------- TASK-007 slice 1: tenant usage snapshot ----------
        // Threshold at which a resource is considered "critical" and a warning is surfaced
        // to the tenant. Kept as a constant here; move to tenant_config later if a client
        // wants to override it per-org.
        private const int CriticalPercent = 80;

        public async Task<TenantUsageDto> GetUsageSnapshotAsync(Guid tenantId)
        {
            // We do the counts and the plan lookup directly here rather than call into
            // ITenantConfigurationService.GetSubscriptionDetailsAsync so this endpoint
            // stays open to every authenticated tenant user (the configuration service
            // sits behind [RequireSuperUser] on its controller).
            var subscription = await _context.TenantSubscriptions
                .IgnoreQueryFilters()
                .Include(s => s.SubscriptionPlan)
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            var vehicleCount = await _context.Vehicles
                .IgnoreQueryFilters()
                .CountAsync(v => v.TenantId == tenantId);

            var userCount = await _context.Users
                .IgnoreQueryFilters()
                .CountAsync(u => u.TenantId == tenantId);

            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthlyShipmentCount = await _context.Shipments
                .IgnoreQueryFilters()
                .CountAsync(s => s.TenantId == tenantId && s.CreatedAt >= startOfMonth);

            // Fallback matches the "Starter Tier" defaults used elsewhere so a fresh tenant
            // without an explicit subscription row still gets sensible numbers.
            var planTier = subscription?.SubscriptionPlan?.Tier ?? "Starter";
            var planStatus = subscription?.Status ?? "Active";
            var maxVehicles = subscription?.SubscriptionPlan?.MaxVehicles ?? 10;
            var maxUsers = subscription?.SubscriptionPlan?.MaxUsers ?? 3;
            var maxMonthlyShipments = subscription?.SubscriptionPlan?.MaxMonthlyShipments ?? 500;

            var vehicles = BuildResource(vehicleCount, maxVehicles);
            var users = BuildResource(userCount, maxUsers);
            var shipments = BuildResource(monthlyShipmentCount, maxMonthlyShipments);

            var warnings = new List<string>();
            if (vehicles.IsCritical)
                warnings.Add($"Fleet quota near limit — {vehicles.Current} of {vehicles.Max} vehicles used ({vehicles.Percent}%).");
            if (users.IsCritical)
                warnings.Add($"User seat quota near limit — {users.Current} of {users.Max} seats used ({users.Percent}%).");
            if (shipments.IsCritical)
                warnings.Add($"Monthly shipment quota near limit — {shipments.Current} of {shipments.Max} shipments this month ({shipments.Percent}%).");

            return new TenantUsageDto
            {
                PlanTier = planTier,
                PlanStatus = planStatus,
                ExpiresAt = subscription?.ExpiresAt,
                Vehicles = vehicles,
                Users = users,
                MonthlyShipments = shipments,
                Warnings = warnings,
            };
        }

        // ------------------------------------------------------------
        // TASK-046 Phase 1 — platform-admin onboarding + RBAC seed helpers.
        // ------------------------------------------------------------

        private static readonly (string Code, string Name, string Description)[] SystemRoleTemplates =
        {
            ("admin",              "Administrator",       "Full tenant management + all enabled modules"),
            ("operations_manager", "Operations Manager",  "Operational modules + reports"),
            ("supervisor",         "Supervisor",          "Approve / edit operational docs"),
            ("operator",           "Operator",            "Create own documents only"),
            ("billing_executive",  "Billing Executive",   "Billing + invoices CRUD"),
            ("accountant",         "Accountant",          "Financial reports + view-only operations"),
            ("viewer",             "Viewer",              "View-only everything the tenant has"),
        };

        public IReadOnlyList<SystemRoleTemplateDto> GetSystemRoleTemplates()
            => SystemRoleTemplates
                .Select(t => new SystemRoleTemplateDto { Code = t.Code, Name = t.Name, Description = t.Description })
                .ToList();

        public async Task<TenantOnboardingResponse> OnboardTenantByAdminAsync(TenantAdminOnboardRequest request, int? platformAdminUserId)
        {
            try
            {
                _logger.LogInformation("Admin-onboarding tenant: {Org} ({Code}) by platform user {Pid}",
                    request.OrganizationName, request.OrganizationCode, platformAdminUserId);

                var normalizedCode = request.OrganizationCode.Trim().ToUpperInvariant();

                var existingTenant = await _context.Tenants
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.Code.ToUpper() == normalizedCode);
                if (existingTenant != null)
                {
                    return new TenantOnboardingResponse { Success = false, Message = $"Organization code '{request.OrganizationCode}' is already registered." };
                }

                var existingUser = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == request.AdminUsername.Trim().ToLower());
                if (existingUser != null)
                {
                    return new TenantOnboardingResponse { Success = false, Message = $"Username '{request.AdminUsername}' is already taken." };
                }

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

                // Admin user — note we DO honor request.AdminRoleCode here
                // (platform-admin-trusted); the string column is dual-written
                // alongside the new user_roles M2M row below (§Y).
                var roleCode = string.IsNullOrWhiteSpace(request.AdminRoleCode) ? "admin" : request.AdminRoleCode.Trim();
                var adminUser = new User
                {
                    TenantId = tenant.Id,
                    Username = request.AdminUsername.Trim(),
                    Password = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
                    FullName = request.AdminFullName.Trim(),
                    Mobile = request.AdminMobile,
                    Email = string.IsNullOrWhiteSpace(request.AdminEmail) ? null : request.AdminEmail.Trim(),
                    Role = roleCode,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Users.Add(adminUser);
                await _context.SaveChangesAsync();

                // Plan link
                var planTier = string.IsNullOrWhiteSpace(request.PlanTier) ? "Starter" : request.PlanTier.Trim();
                var plan = await _context.SubscriptionPlans
                    .FirstOrDefaultAsync(p => p.Tier.ToLower() == planTier.ToLower() || p.Name.ToLower() == planTier.ToLower());
                if (plan == null)
                {
                    plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Tier == "Starter");
                }
                if (plan != null)
                {
                    _context.TenantSubscriptions.Add(new TenantSubscription
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        SubscriptionPlanId = plan.Id,
                        Status = "Active",
                        StartedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddYears(1),
                        IsAutoRenew = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // enabled_feature_keys stays populated per §Y for rollback —
                // dual-write from the EnabledModuleCodes list.
                var enabledKeys = (request.EnabledModuleCodes ?? new List<string>())
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Select(k => k.Trim().ToLowerInvariant())
                    .Distinct()
                    .ToList();
                if (!enabledKeys.Contains("dashboard")) enabledKeys.Insert(0, "dashboard");

                _context.TenantEntitlementSubscriptions.Add(new TenantEntitlementSubscription
                {
                    TenantId = tenant.Id,
                    PlanTier = planTier,
                    EnabledFeatureKeys = enabledKeys,
                    EffectiveFrom = DateTime.UtcNow,
                    EffectiveUntil = null,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = platformAdminUserId
                });

                // tenant_settings for other config
                _context.TenantSettings.Add(new TenantSetting
                {
                    TenantId = tenant.Id,
                    GeneralJson = JsonSerializer.Serialize(new { companyName = tenant.Name, companyCode = tenant.Code }, JsonOptions),
                    BillingAndTaxJson = JsonSerializer.Serialize(new { enableGstBilling = true, defaultGstRate = 5.0m }, JsonOptions),
                    DocumentSequencesJson = "{}",
                    OperationalWorkflowsJson = "{}",
                    FeatureFlagsJson = "{}",
                    IntegrationsJson = "{}",
                    CustomSettingsJson = "{}",
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                // Seed system roles + tenant_modules + the admin user_role row.
                await SeedTenantModulesAsync(tenant.Id, enabledKeys, adminUser.Id);
                await SeedSystemRolesForTenantAsync(tenant.Id, enabledKeys, platformAdminUserId ?? adminUser.Id);
                await AssignUserRoleAsync(tenant.Id, adminUser.Id, roleCode, assignedBy: platformAdminUserId);
                await _context.SaveChangesAsync();

                await _auditLogService.LogAsync("TenantCreatedByPlatformAdmin", tenantId: tenant.Id,
                    entityType: "Tenant", entityId: tenant.Id.ToString(),
                    details: $"Platform admin {platformAdminUserId} onboarded tenant {tenant.Code} with admin role '{roleCode}' and modules [{string.Join(",", enabledKeys)}]");

                var token = GenerateJwtToken(adminUser);

                return new TenantOnboardingResponse
                {
                    Success = true,
                    Message = "Tenant onboarded by platform admin.",
                    TenantId = tenant.Id,
                    OrganizationName = tenant.Name,
                    OrganizationCode = tenant.Code,
                    PlanTier = plan?.Tier ?? planTier,
                    EnabledModules = enabledKeys,
                    Token = token,
                    AdminUser = new UserDto
                    {
                        Id = adminUser.Id,
                        Username = adminUser.Username,
                        FullName = adminUser.FullName ?? string.Empty,
                        Role = adminUser.Role ?? roleCode,
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

        /// <summary>
        /// TASK-046 Phase 1 — insert 7 system roles for a tenant with
        /// seed_grants scoped to the modules the tenant has enabled.
        /// Idempotent via the unique indexes on roles / role_permissions.
        /// Caller must SaveChangesAsync after this returns.
        /// </summary>
        private async Task SeedSystemRolesForTenantAsync(Guid tenantId, List<string> enabledFeatureKeys, int? actingUserId)
        {
            // 1. ensure the 7 role rows exist.
            var existingCodes = await _context.Roles
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId)
                .Select(r => r.Code.ToLower())
                .ToListAsync();
            var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

            var roleIdByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (code, name, desc) in SystemRoleTemplates)
            {
                if (existingSet.Contains(code)) continue;
                var role = new Role
                {
                    TenantId = tenantId,
                    Code = code,
                    Name = name,
                    Description = desc,
                    IsSystem = true,
                    IsActive = true,
                    CreatedBy = actingUserId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Roles.Add(role);
            }
            await _context.SaveChangesAsync();

            foreach (var r in await _context.Roles.IgnoreQueryFilters().Where(r => r.TenantId == tenantId).ToListAsync())
            {
                roleIdByCode[r.Code.ToLowerInvariant()] = r.Id;
            }

            // 2. seed role_permissions grants — only insert where no active row exists.
            var catalog = await _context.Permissions.AsNoTracking().ToListAsync();
            var alreadyGranted = await _context.RolePermissions
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId && r.RevokedAt == null)
                .Select(r => new { r.RoleName, r.PermissionKey })
                .ToListAsync();
            var granted = new HashSet<string>(alreadyGranted.Select(a => $"{a.RoleName.ToLowerInvariant()}|{a.PermissionKey}"));

            // enabled modules (code set) — if tenant has no tenant_modules yet, derive from the feature-key list.
            var enabledModuleCodes = (await _context.TenantModules
                .IgnoreQueryFilters()
                .Where(tm => tm.TenantId == tenantId && tm.EnabledUntil == null && tm.IsEnabled)
                .Join(_context.Modules.IgnoreQueryFilters(), tm => tm.ModuleId, m => m.Id, (tm, m) => m.Code)
                .ToListAsync());
            if (enabledModuleCodes.Count == 0)
            {
                enabledModuleCodes = enabledFeatureKeys
                    .Select(k => ResolveModuleCodeFromFeatureKey(k))
                    .Where(c => c != null)
                    .Select(c => c!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            var enabledModules = new HashSet<string>(enabledModuleCodes, StringComparer.OrdinalIgnoreCase);

            foreach (var perm in catalog)
            {
                var moduleCode = ResolveModuleCodeFromFeatureKey(perm.FeatureKey);
                if (moduleCode == null || !enabledModules.Contains(moduleCode)) continue;

                foreach (var (code, _, _) in SystemRoleTemplates)
                {
                    if (!PermissionFitsRoleSeedGrant(code, moduleCode, perm.Action)) continue;
                    if (!roleIdByCode.TryGetValue(code, out var roleId)) continue;
                    var sig = $"{code}|{perm.Key}";
                    if (granted.Contains(sig)) continue;
                    _context.RolePermissions.Add(new RolePermission
                    {
                        TenantId = tenantId,
                        RoleName = code,       // §Y dual-write — stays forever.
                        RoleId = roleId,
                        PermissionKey = perm.Key,
                        GrantedAt = DateTime.UtcNow,
                        GrantedBy = actingUserId
                    });
                    granted.Add(sig);
                }
            }
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// TASK-046 Phase 1 — map a feature key to its root module code. Mirrors
        /// the shim in <see cref="EntitlementsService"/>.
        /// </summary>
        private static string? ResolveModuleCodeFromFeatureKey(string featureKey)
        {
            if (string.IsNullOrWhiteSpace(featureKey)) return null;
            var fk = featureKey.ToLowerInvariant();
            if (fk.StartsWith("consignments")) return "bilty";
            if (fk == "delivery_settlement") return "delivery_settlement";
            if (fk == "trip_settlement") return "trip_settlement";
            if (fk.StartsWith("empty_trips") || fk.StartsWith("trips")) return "trips";
            if (fk.StartsWith("billing")) return "billing";
            if (fk.StartsWith("pod")) return "pod";
            if (fk.StartsWith("reports")) return "reports";
            if (fk.StartsWith("master_data")) return "master_data";
            if (fk.StartsWith("quotations")) return "quotations";
            if (fk.StartsWith("vendors")) return "vendors";
            if (fk.StartsWith("claims")) return "claims";
            if (fk.StartsWith("tracking")) return "tracking";
            if (fk.StartsWith("analytics")) return "analytics";
            if (fk.StartsWith("dashboard") || fk == "bilty") return fk.StartsWith("dashboard") ? "dashboard" : "bilty";
            if (fk.StartsWith("system")) return "system";
            if (fk == "clients") return "system";
            return null;
        }

        private static bool PermissionFitsRoleSeedGrant(string roleCode, string moduleCode, string action)
        {
            // Matches the SQL rules in BackfillRolesAndUserRoles.
            switch (roleCode.ToLowerInvariant())
            {
                case "admin":
                    return true;
                case "operations_manager":
                    if (moduleCode is "bilty" or "trips" or "pod" or "trip_settlement" or "delivery_settlement" or "dashboard") return true;
                    if (moduleCode == "reports" && (action == "View" || action == "Export")) return true;
                    return false;
                case "supervisor":
                    if (moduleCode == "bilty" && action is "View" or "Edit" or "Approve") return true;
                    if (moduleCode == "pod" && action is "View" or "Edit" or "Approve") return true;
                    if (moduleCode == "dashboard" && action == "View") return true;
                    return false;
                case "operator":
                    if (moduleCode == "bilty" && action is "View" or "Create") return true;
                    if (moduleCode == "pod" && action is "View" or "Create") return true;
                    if (moduleCode == "dashboard" && action == "View") return true;
                    return false;
                case "billing_executive":
                    if (moduleCode == "billing") return true;
                    if (moduleCode == "bilty" && action == "View") return true;
                    if (moduleCode == "dashboard" && action == "View") return true;
                    return false;
                case "accountant":
                    if (moduleCode == "reports") return true;
                    if (moduleCode is "bilty" or "trips" or "billing" && action == "View") return true;
                    if (moduleCode == "dashboard" && action == "View") return true;
                    return false;
                case "viewer":
                    return action == "View";
                default:
                    return false;
            }
        }

        /// <summary>
        /// TASK-046 Phase 1 — tenant_modules seed from EnabledModuleCodes.
        /// Idempotent — skips any (tenant, module) with an active row already.
        /// </summary>
        private async Task SeedTenantModulesAsync(Guid tenantId, List<string> moduleCodes, int? actingUserId)
        {
            if (moduleCodes.Count == 0) return;
            var catalog = await _context.Modules.AsNoTracking().ToListAsync();
            var byCode = catalog.ToDictionary(m => m.Code.ToLowerInvariant(), m => m.Id);
            var existingActive = await _context.TenantModules
                .IgnoreQueryFilters()
                .Where(tm => tm.TenantId == tenantId && tm.EnabledUntil == null)
                .Select(tm => tm.ModuleId)
                .ToListAsync();
            var existingSet = new HashSet<int>(existingActive);

            foreach (var code in moduleCodes.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!byCode.TryGetValue(code.ToLowerInvariant(), out var moduleId)) continue;
                if (existingSet.Contains(moduleId)) continue;
                _context.TenantModules.Add(new TenantModule
                {
                    TenantId = tenantId,
                    ModuleId = moduleId,
                    IsEnabled = true,
                    EnabledFrom = DateTime.UtcNow,
                    EnabledUntil = null,
                    EnabledBy = actingUserId,
                    CreatedAt = DateTime.UtcNow
                });
                existingSet.Add(moduleId);
            }
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// TASK-046 Phase 1 — like <see cref="SeedTenantModulesAsync"/> but
        /// takes legacy feature keys (e.g. "billing.bill_book") instead of
        /// module codes.
        /// </summary>
        private async Task SeedTenantModulesFromFeatureKeysAsync(Guid tenantId, List<string> featureKeys, int? actingUserId)
        {
            var codes = featureKeys
                .Select(k => ResolveModuleCodeFromFeatureKey(k))
                .Where(c => c != null)
                .Select(c => c!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            await SeedTenantModulesAsync(tenantId, codes, actingUserId);
        }

        /// <summary>
        /// TASK-046 Phase 1 — insert a user_roles M2M row for (tenantId, userId,
        /// role.code). Also dual-writes users.role string per §Y so the legacy
        /// path keeps working. Idempotent via partial unique index.
        /// </summary>
        private async Task AssignUserRoleAsync(Guid tenantId, int userId, string roleCode, int? assignedBy)
        {
            if (string.IsNullOrWhiteSpace(roleCode)) return;
            var role = await _context.Roles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Code.ToLower() == roleCode.ToLower());
            if (role == null)
            {
                // custom role — create with IsSystem=false. Keeps users.role string valid.
                role = new Role
                {
                    TenantId = tenantId,
                    Code = roleCode.Trim().ToLowerInvariant(),
                    Name = roleCode.Trim(),
                    Description = "Custom role",
                    IsSystem = false,
                    IsActive = true,
                    CreatedBy = assignedBy,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Roles.Add(role);
                await _context.SaveChangesAsync();
            }

            var existing = await _context.UserRoles
                .IgnoreQueryFilters()
                .AnyAsync(ur => ur.TenantId == tenantId && ur.UserId == userId && ur.RoleId == role.Id && ur.RevokedAt == null);
            if (!existing)
            {
                _context.UserRoles.Add(new UserRole
                {
                    TenantId = tenantId,
                    UserId = userId,
                    RoleId = role.Id,
                    AssignedAt = DateTime.UtcNow,
                    AssignedBy = assignedBy
                });
            }
        }

        // ------------------------------------------------------------

        private static ResourceUsageDto BuildResource(int current, int max)
        {
            // Percent is clamped to 0..100 and rounded so the UI can render a bar directly.
            // A max of 0 shouldn't happen in practice (Starter defaults are non-zero) but
            // guard against divide-by-zero anyway.
            int percent = max <= 0 ? 0 : (int)Math.Min(100, Math.Round((decimal)current * 100 / max));
            return new ResourceUsageDto
            {
                Current = current,
                Max = max,
                Percent = percent,
                IsCritical = percent >= CriticalPercent,
            };
        }
    }
}
