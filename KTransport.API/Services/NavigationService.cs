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
    public class NavigationService : INavigationService
    {
        private readonly KTransportDbContext _context;
        private readonly IEntitlementsService? _entitlements;
        private readonly ILogger<NavigationService>? _log;
        private readonly IMenuCatalogService _menuCatalog;
        private readonly IHttpContextAccessor? _httpContextAccessor;

        // Legacy ctor kept for existing tests that only pass a DbContext.
        // TASK-045 Phase 3: the menu catalog service is now the sole production
        // path, so the legacy ctor instantiates one directly to keep those
        // tests exercising the real builder.
        public NavigationService(KTransportDbContext context)
        {
            _context = context;
            _menuCatalog = new MenuCatalogService(context, NullLogger<MenuCatalogService>.Instance);
        }

        // TASK-044 Phase 3: EntitlementsOptions removed — tables are the sole source.
        public NavigationService(
            KTransportDbContext context,
            IEntitlementsService? entitlements,
            ILogger<NavigationService>? log)
        {
            _context = context;
            _entitlements = entitlements;
            _log = log;
            _menuCatalog = new MenuCatalogService(context, NullLogger<MenuCatalogService>.Instance);
        }

        // Full constructor used by DI. TASK-045 Phase 3: dual-read options param removed.
        public NavigationService(
            KTransportDbContext context,
            IEntitlementsService? entitlements,
            ILogger<NavigationService>? log,
            IMenuCatalogService menuCatalog)
        {
            _context = context;
            _entitlements = entitlements;
            _log = log;
            _menuCatalog = menuCatalog ?? new MenuCatalogService(context, NullLogger<MenuCatalogService>.Instance);
        }

        // Full constructor with HttpContext (DI-selected).
        public NavigationService(
            KTransportDbContext context,
            IEntitlementsService? entitlements,
            ILogger<NavigationService>? log,
            IMenuCatalogService menuCatalog,
            IHttpContextAccessor? httpContextAccessor)
            : this(context, entitlements, log, menuCatalog)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<List<DynamicMenuItemDto>> GetDynamicMenuAsync(Guid tenantId, string userRole, string? userIdOrName = null)
        {
            var entitlements = await GetTenantMenuEntitlementsAsync(tenantId);

            // 1. Organization-Level Subscribed Keys
            var subscribedKeys = new HashSet<string>(entitlements.EnabledMenuKeys, StringComparer.OrdinalIgnoreCase);

            // TASK-046 Phase 1 bypass fix: ONLY the platform-operator SUPER_USER role
            // short-circuits to the wildcard. admin / tenantadmin / superadmin /
            // TENANT_OWNER are now tenant-scoped roles subject to the normal
            // role_permissions + user_permission_overrides + tenant_modules
            // intersection pipeline. See .agent/specs/decisions/authorization-rbac-architecture.md §F.
            bool isSuperUser = string.Equals(userRole, "SUPER_USER", StringComparison.OrdinalIgnoreCase);

            HashSet<string> effectiveKeys;

            if (isSuperUser)
            {
                // Super User gets all organization-subscribed features + settings & dashboard
                effectiveKeys = new HashSet<string>(subscribedKeys, StringComparer.OrdinalIgnoreCase);
                effectiveKeys.Add("dashboard");
                effectiveKeys.Add("system");
                effectiveKeys.Add("system.settings");
                effectiveKeys.Add("*");
                if (tenantId == TenantConstants.DefaultTenantId)
                {
                    effectiveKeys.Add("clients");
                }
            }
            else if (_entitlements != null)
            {
                // TASK-049 Option B: tables are the sole authoritative source.
                // The structural ComputeEffective returns the full set of
                // effective permission keys (e.g. "billing.invoices.view").
                // Menu visibility then checks menu_items.permission_key /
                // menu_items.key against that set — no prefix-expansion,
                // no NormalizeFeatureKey. The 40-line expansion loop is
                // deleted; its only purpose was to feed the old fuzzy
                // IsVisible matcher.
                var resolvedUserId = await ResolveUserIdAsync(tenantId, userIdOrName);
                var tablesActionKeys = await _entitlements.ComputeEffectivePermissionsFromTablesAsync(tenantId, resolvedUserId, userRole);

                effectiveKeys = new HashSet<string>(tablesActionKeys, StringComparer.OrdinalIgnoreCase);

                // For each granted permission key also index its feature-key
                // prefix so menu_items rows whose `key` matches the feature
                // (e.g. row key='billing.invoices', permission='billing.invoices.view')
                // stay visible. One single prefix hop; no legacy alias magic.
                foreach (var k in tablesActionKeys)
                {
                    if (string.IsNullOrWhiteSpace(k)) continue;
                    var lastDot = k.LastIndexOf('.');
                    if (lastDot > 0)
                    {
                        var featurePrefix = k.Substring(0, lastDot);
                        effectiveKeys.Add(featurePrefix);
                        // Root prefix too (so a 3-dot key like billing.invoices.view
                        // also surfaces 'billing' for the parent-group row).
                        var firstDot = k.IndexOf('.');
                        if (firstDot > 0 && firstDot < lastDot)
                        {
                            effectiveKeys.Add(k.Substring(0, firstDot));
                        }
                    }
                }

                // Active sub-users always get Dashboard
                effectiveKeys.Add("dashboard");
                effectiveKeys.Add("dashboard.view");

                // Sub-users NEVER receive SaaS configuration or superadmin screens
                effectiveKeys.Remove("system.settings");
                effectiveKeys.Remove("system.onboard");
                effectiveKeys.Remove("clients");
                effectiveKeys.Remove("saas");
                effectiveKeys.Remove("SAAS_CONFIGURATION");
            }
            else
            {
                // Entitlements service not wired (legacy constructors used in some tests):
                // fall back to the organization-subscribed set as the effective view.
                effectiveKeys = new HashSet<string>(subscribedKeys, StringComparer.OrdinalIgnoreCase);
                effectiveKeys.Add("dashboard");
                effectiveKeys.Add("dashboard.view");
            }

            // TASK-045 Phase 3: menu_items table is the sole authoritative source.
            // The DualReadMode harness and the ~550-line C# if/else fallback are gone.
            return await _menuCatalog.BuildMenuFromTablesAsync(effectiveKeys, entitlements);
        }

        private async Task<int?> ResolveUserIdAsync(Guid tenantId, string? userIdOrName)
        {
            if (string.IsNullOrWhiteSpace(userIdOrName)) return null;
            if (int.TryParse(userIdOrName, out var uid)) return uid;
            var u = await _context.Users
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && x.Username == userIdOrName)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();
            return u;
        }

        public async Task<List<string>> GetUserPermissionsAsync(Guid tenantId, string userRole, string? userIdOrName = null)
        {
            // TASK-046 Phase 1 bypass fix: only the platform-operator SUPER_USER
            // role still wildcards here. Every other role goes through the
            // tenant-scoped permissions pipeline.
            bool isSuperUserCaller = string.Equals(userRole, "SUPER_USER", StringComparison.OrdinalIgnoreCase);

            // TASK-044 Phase 3: tables are authoritative. Sub-users get the action-split
            // permission set directly from role_permissions / user_permission_overrides.
            if (!isSuperUserCaller && _entitlements != null)
            {
                // TASK-049 Option B: structural compute, then one feature-key
                // prefix hop so UI code that still queries `billing` surfaces
                // the parent group when any `billing.*.*` leaf is granted.
                var resolvedUserId = await ResolveUserIdAsync(tenantId, userIdOrName);
                var tablesActionKeys = await _entitlements.ComputeEffectivePermissionsFromTablesAsync(tenantId, resolvedUserId, userRole);
                var result = new HashSet<string>(tablesActionKeys, StringComparer.OrdinalIgnoreCase);
                foreach (var k in tablesActionKeys)
                {
                    if (string.IsNullOrWhiteSpace(k)) continue;
                    var lastDot = k.LastIndexOf('.');
                    if (lastDot > 0) result.Add(k.Substring(0, lastDot));
                    var firstDot = k.IndexOf('.');
                    if (firstDot > 0 && firstDot < lastDot) result.Add(k.Substring(0, firstDot));
                }
                result.Add("dashboard");
                result.Add("dashboard.view");
                return result.ToList();
            }

            var menu = await GetDynamicMenuAsync(tenantId, userRole, userIdOrName);
            var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void CollectPermissions(DynamicMenuItemDto item)
            {
                if (!string.IsNullOrWhiteSpace(item.PermissionKey)) permissions.Add(item.PermissionKey);
                if (!string.IsNullOrWhiteSpace(item.Id)) permissions.Add(item.Id);
                if (item.Children != null)
                {
                    foreach (var child in item.Children) CollectPermissions(child);
                }
            }

            foreach (var item in menu) CollectPermissions(item);

            if (isSuperUserCaller)
            {
                permissions.Add("*");
                permissions.Add("admin");
                permissions.Add("settings.manage");
                permissions.Add("users.manage");
                permissions.Add("saas.tenants.manage");
            }

            return permissions.ToList();
        }

        /// <summary>
        /// TASK-044 Phase 3: tenant entitlements are reconstructed from the normalized
        /// tables (tenant_entitlement_subscriptions + tenant_report_entitlements) rather
        /// than parsed from the dropped menu_entitlements_json column.
        /// </summary>
        public async Task<TenantMenuEntitlementsDto> GetTenantMenuEntitlementsAsync(Guid tenantId)
        {
            var subscription = await _context.TenantEntitlementSubscriptions
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId && s.EffectiveUntil == null)
                .OrderByDescending(s => s.EffectiveFrom)
                .FirstOrDefaultAsync();

            var reportRows = await _context.TenantReportEntitlements
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId)
                .ToListAsync();

            if (subscription == null && reportRows.Count == 0)
            {
                return GetDefaultEntitlements(tenantId);
            }

            var catalog = GetStandardReportCatalog();
            var reportStateByKey = reportRows.ToDictionary(r => r.ReportKey, r => r.IsEnabled, StringComparer.OrdinalIgnoreCase);
            foreach (var rep in catalog)
            {
                if (reportStateByKey.TryGetValue(rep.ReportKey, out var isEnabled))
                {
                    rep.IsEnabled = isEnabled;
                }
                else
                {
                    rep.IsEnabled = false;
                }
            }

            // TASK-049 Option B: derive ModuleCodes from tenant_modules
            // (authoritative). EnabledMenuKeys is kept populated as the legacy
            // mirror for Phase A readers.
            var moduleCodes = await (
                from tm in _context.TenantModules.IgnoreQueryFilters()
                join m in _context.Modules.IgnoreQueryFilters() on tm.ModuleId equals m.Id
                where tm.TenantId == tenantId && tm.EnabledUntil == null
                orderby m.Code
                select m.Code
            ).ToListAsync();

            // TASK-049b: ModuleCodes derived from tenant_modules is authoritative.
            // When tenant_modules has rows, EnabledMenuKeys mirrors ModuleCodes
            // directly (ignoring any stale dotted keys in subscription.EnabledFeatureKeys).
            // When tenant_modules is empty, fall back to EnabledFeatureKeys for Phase A
            // legacy test compatibility (matching EntitlementsService tmSet.Count == 0 fallback).
            var effectiveMenuKeys = moduleCodes.Count > 0
                ? moduleCodes
                : (subscription?.EnabledFeatureKeys?.ToList() ?? moduleCodes);

            var dto = new TenantMenuEntitlementsDto
            {
                TenantId = tenantId,
                PlanTier = subscription?.PlanTier ?? "Enterprise",
                ModuleCodes = moduleCodes,
                EnabledMenuKeys = effectiveMenuKeys,
                Reports = catalog
            };
            return dto;
        }

        public async Task<TenantMenuEntitlementsDto> UpdateTenantMenuEntitlementsAsync(Guid tenantId, TenantMenuEntitlementsDto dto)
        {
            dto.TenantId = tenantId;

            // Ensure a TenantSetting row exists (other fields on it still matter) but DO NOT
            // write to the dropped menu_entitlements_json column.
            var setting = await _context.TenantSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (setting == null)
            {
                setting = new TenantSetting
                {
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.TenantSettings.Add(setting);
            }
            else
            {
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Persist to the normalized tables (sole source of truth in Phase 3).
            if (_entitlements != null)
            {
                try
                {
                    int? actingUserId = null;
                    var principal = _httpContextAccessor?.HttpContext?.User;
                    var sub = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? principal?.FindFirst("sub")?.Value;
                    if (!string.IsNullOrWhiteSpace(sub) && int.TryParse(sub, out var uid))
                    {
                        actingUserId = uid;
                    }
                    await _entitlements.WriteTenantEntitlementsAsync(tenantId, dto, actingUserId);
                }
                catch (Exception ex)
                {
                    _log?.LogWarning(ex, "WriteTenantEntitlementsAsync failed tenant={TenantId}", tenantId);
                }
            }

            return dto;
        }

        private List<string> GetDefaultMenuKeys()
        {
            return new List<string>
            {
                "dashboard", "consignments", "consignments.create", "consignments.all",
                "delivery_settlement", "quotations", "trips", "trip_settlement", "empty_trips",
                "pod", "billing", "billing.bill_book", "bill_book", "billing.invoices", "billing.receipts",
                "master_data", "master_data.parties", "master_data.fleet", "master_data.compliance",
                "master_data.tyres", "master_data.spares", "master_data.loans", "master_data.driver_ledger",
                "master_data.vehicle_claims", "master_data.rates", "master_data.vendor_rates",
                "vendors", "claims", "analytics", "reports", "reports.booking_register",
                "reports.tax_summary", "reports.party_outstanding", "reports.trip_profitability",
                "reports.vendor_payables", "tracking", "clients", "system", "system.settings",
                "system.onboard", "system.forgot_password"
            };
        }

        private TenantMenuEntitlementsDto GetDefaultEntitlements(Guid tenantId)
        {
            return new TenantMenuEntitlementsDto
            {
                TenantId = tenantId,
                PlanTier = "Enterprise",
                EnabledMenuKeys = GetDefaultMenuKeys(),
                Reports = GetStandardReportCatalog()
            };
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
