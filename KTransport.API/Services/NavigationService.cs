using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KTransport.API.Services
{
    /// <summary>
    /// RBAC clean-slate: navigation serves the server-computed menu and the
    /// effective permission set. SUPER_USER (platform operator) wildcards; every
    /// other role flows through the single structural engine.
    /// </summary>
    public class NavigationService : INavigationService
    {
        private readonly KTransportDbContext _context;
        private readonly IEntitlementsService? _entitlements;
        private readonly ILogger<NavigationService>? _log;
        private readonly IMenuCatalogService _menuCatalog;
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public NavigationService(KTransportDbContext context)
        {
            _context = context;
            _menuCatalog = new MenuCatalogService(context, NullLogger<MenuCatalogService>.Instance);
        }

        public NavigationService(KTransportDbContext context, IEntitlementsService? entitlements, ILogger<NavigationService>? log)
        {
            _context = context;
            _entitlements = entitlements;
            _log = log;
            _menuCatalog = new MenuCatalogService(context, NullLogger<MenuCatalogService>.Instance);
        }

        public NavigationService(KTransportDbContext context, IEntitlementsService? entitlements, ILogger<NavigationService>? log, IMenuCatalogService menuCatalog)
        {
            _context = context;
            _entitlements = entitlements;
            _log = log;
            _menuCatalog = menuCatalog ?? new MenuCatalogService(context, NullLogger<MenuCatalogService>.Instance);
        }

        public NavigationService(KTransportDbContext context, IEntitlementsService? entitlements, ILogger<NavigationService>? log, IMenuCatalogService menuCatalog, IHttpContextAccessor? httpContextAccessor)
            : this(context, entitlements, log, menuCatalog)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private static bool IsSuperUser(string? userRole) =>
            string.Equals(userRole, RbacRoles.PlatformSuperUser, StringComparison.OrdinalIgnoreCase);

        public async Task<List<DynamicMenuItemDto>> GetDynamicMenuAsync(Guid tenantId, string userRole, string? userIdOrName = null)
        {
            var entitlements = await GetTenantMenuEntitlementsAsync(tenantId);
            HashSet<string> effectiveKeys;

            if (IsSuperUser(userRole))
            {
                effectiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*", "dashboard", "system", "system.settings" };
                if (tenantId == TenantConstants.DefaultTenantId) effectiveKeys.Add("clients");
            }
            else if (_entitlements != null)
            {
                var resolvedUserId = await ResolveUserIdAsync(tenantId, userIdOrName);
                var keys = await _entitlements.ComputeEffectivePermissionsFromTablesAsync(tenantId, resolvedUserId, userRole);
                effectiveKeys = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase)
                {
                    "dashboard", "dashboard.view"
                };
                ApplyAdminMenuGates(effectiveKeys, userRole, resolvedUserId, tenantId);
            }
            else
            {
                effectiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "dashboard", "dashboard.view" };
            }

            return await _menuCatalog.BuildMenuFromTablesAsync(effectiveKeys, entitlements);
        }

        public async Task<List<string>> GetUserPermissionsAsync(Guid tenantId, string userRole, string? userIdOrName = null)
        {
            if (IsSuperUser(userRole))
            {
                return new List<string> { "*", "admin", "settings.manage", "users.manage", "system.settings", "saas.tenants.manage", "dashboard" };
            }

            if (_entitlements != null)
            {
                var resolvedUserId = await ResolveUserIdAsync(tenantId, userIdOrName);
                var keys = await _entitlements.ComputeEffectivePermissionsFromTablesAsync(tenantId, resolvedUserId, userRole);
                var result = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase) { "dashboard", "dashboard.view" };
                ApplyAdminMenuGates(result, userRole, resolvedUserId, tenantId);
                return result.ToList();
            }

            return new List<string> { "dashboard", "dashboard.view" };
        }

        /// <summary>
        /// A genuine tenant admin keeps org-settings + user/role management screens.
        /// These are menu-gating keys with no catalog permission row, so they are
        /// injected for admin-class callers only. "Admin" is determined by a real
        /// user_roles → roles.code assignment (never the JWT string alone).
        /// </summary>
        private void ApplyAdminMenuGates(HashSet<string> keys, string? userRole, int? userId, Guid tenantId)
        {
            bool isAdmin = IsGenuineAdmin(tenantId, userId);
            if (isAdmin)
            {
                keys.Add("system");
                keys.Add("system.settings");
                keys.Add("settings.manage");
                keys.Add("users.manage");
            }
            else
            {
                keys.Remove("system.settings");
                keys.Remove("settings.manage");
                keys.Remove("users.manage");
            }
            keys.Remove("system.onboard");
            keys.Remove("clients");
        }

        private bool IsGenuineAdmin(Guid tenantId, int? userId)
        {
            if (!userId.HasValue) return false;
            return (from ur in _context.UserRoles.IgnoreQueryFilters()
                    join r in _context.Roles.IgnoreQueryFilters() on ur.RoleId equals r.Id
                    where ur.TenantId == tenantId && ur.UserId == userId.Value && ur.RevokedAt == null
                          && r.IsActive && RbacRoles.AdminRoleCodeList.Contains(r.Code.ToLower())
                    select ur.Id).Any();
        }

        private async Task<int?> ResolveUserIdAsync(Guid tenantId, string? userIdOrName)
        {
            if (string.IsNullOrWhiteSpace(userIdOrName)) return null;
            if (int.TryParse(userIdOrName, out var uid)) return uid;
            return await _context.Users
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && x.Username == userIdOrName)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();
        }

        public async Task<TenantMenuEntitlementsDto> GetTenantMenuEntitlementsAsync(Guid tenantId)
        {
            var moduleCodes = await (
                from tm in _context.TenantModules.IgnoreQueryFilters()
                join m in _context.Modules.IgnoreQueryFilters() on tm.ModuleId equals m.Id
                where tm.TenantId == tenantId && tm.EnabledUntil == null
                orderby m.Code
                select m.Code).ToListAsync();

            var pageRows = await (
                from p in _context.TenantModulePages.IgnoreQueryFilters()
                join m in _context.Modules.IgnoreQueryFilters() on p.ModuleId equals m.Id
                where p.TenantId == tenantId && p.EnabledUntil == null
                select new { ModuleCode = m.Code, p.PageKey }).ToListAsync();
            var pageRestrictions = pageRows
                .GroupBy(x => x.ModuleCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.PageKey).Distinct().ToList(), StringComparer.OrdinalIgnoreCase);

            // Reports catalog ∩ tenant report entitlements.
            var reportRows = await _context.TenantReportEntitlements.IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId)
                .ToListAsync();
            var reportStateByKey = reportRows.ToDictionary(r => r.ReportKey, r => r.IsEnabled, StringComparer.OrdinalIgnoreCase);
            var catalog = GetStandardReportCatalog();
            foreach (var rep in catalog)
                rep.IsEnabled = reportStateByKey.TryGetValue(rep.ReportKey, out var en) && en;

            // Assigned plan code (from the billing subscription), if any.
            var planCode = await (
                from s in _context.TenantSubscriptions.IgnoreQueryFilters()
                join p in _context.SubscriptionPlans on s.SubscriptionPlanId equals p.Id
                where s.TenantId == tenantId
                orderby s.StartedAt descending
                select p.Tier).FirstOrDefaultAsync();

            return new TenantMenuEntitlementsDto
            {
                TenantId = tenantId,
                PlanCode = planCode,
                ModuleCodes = moduleCodes,
                PageRestrictions = pageRestrictions,
                Reports = catalog
            };
        }

        public async Task<TenantMenuEntitlementsDto> UpdateTenantMenuEntitlementsAsync(Guid tenantId, TenantMenuEntitlementsDto dto)
        {
            dto.TenantId = tenantId;

            if (_entitlements != null)
            {
                int? actingUserId = null;
                var principal = _httpContextAccessor?.HttpContext?.User;
                var sub = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal?.FindFirst("sub")?.Value;
                if (!string.IsNullOrWhiteSpace(sub) && int.TryParse(sub, out var uid)) actingUserId = uid;
                await _entitlements.WriteTenantEntitlementsAsync(tenantId, dto, actingUserId);
            }

            return await GetTenantMenuEntitlementsAsync(tenantId);
        }

        private List<ReportEntitlementItemDto> GetStandardReportCatalog()
        {
            return new List<ReportEntitlementItemDto>
            {
                new() { ReportKey = "booking_register", Title = "Consignment Booking Register", Description = "Comprehensive log of all booked goods receipts with weight, freight, and consignor breakdown", Category = "Operational", Path = "/reports?tab=booking_register", IsEnabled = true },
                new() { ReportKey = "tax_summary", Title = "GST & Tax Summary Report", Description = "Taxable amounts, CGST, SGST, IGST, and RCM breakdowns for monthly GST returns", Category = "Financial", Path = "/reports?tab=tax_summary", IsEnabled = true },
                new() { ReportKey = "party_outstanding", Title = "Customer Outstanding Ledger", Description = "Real-time receivables, billed vs settled balance, and customer aging summary", Category = "Financial", Path = "/reports?tab=party_outstanding", IsEnabled = true },
                new() { ReportKey = "trip_profitability", Title = "Trip Profitability & P&L", Description = "Trip revenue vs diesel, toll, driver advance, and vehicle expense margin analysis", Category = "Operational", Path = "/reports?tab=trip_profitability", IsEnabled = true },
                new() { ReportKey = "vendor_payables", Title = "Vendor & Lorry Hire Payables", Description = "Transporter / vehicle broker hiring contracts, paid advances, and pending dues", Category = "Financial", Path = "/reports?tab=vendor_payables", IsEnabled = true }
            };
        }
    }
}
