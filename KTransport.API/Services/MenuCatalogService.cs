using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KTransport.API.Services
{
    /// <summary>
    /// RBAC clean-slate (ADR rbac-menu-consolidation-clean-slate): menu rendering
    /// is a PURE structural join on menu_items.module_id / menu_items.parent_id,
    /// gated by tenant_modules (module), tenant_module_pages (page), and the
    /// user's effective permission set (page/module). No string/alias matching,
    /// no feature_key fallback.
    /// </summary>
    public class MenuCatalogService : IMenuCatalogService
    {
        private readonly KTransportDbContext _db;
        private readonly ILogger<MenuCatalogService> _log;

        public MenuCatalogService(KTransportDbContext db, ILogger<MenuCatalogService> log)
        {
            _db = db;
            _log = log;
        }

        public async Task<List<MenuItemDto>> GetAllAsync()
        {
            var rows = await _db.MenuItems.AsNoTracking().OrderBy(m => m.DisplayOrder).ToListAsync();
            var keyById = rows.ToDictionary(r => r.Id, r => r.Key);
            return rows
                .OrderBy(m => m.ParentId.HasValue ? 1 : 0)
                .ThenBy(m => m.DisplayOrder)
                .Select(m => ToDto(m, keyById))
                .ToList();
        }

        [Obsolete("Dual-read harness removed. Thin wrapper kept for interface compatibility.")]
        public async Task<HashSet<string>> BuildMenuIdSetFromTablesAsync(
            Guid tenantId, int? userId, string? userRole, IReadOnlyCollection<string> effectivePermissionKeys)
        {
            var entitlements = new TenantMenuEntitlementsDto { TenantId = tenantId, Reports = new() };
            var effective = new HashSet<string>(effectivePermissionKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var menu = await BuildMenuFromTablesAsync(effective, entitlements);
            var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Collect(DynamicMenuItemDto i) { emitted.Add(i.Id); if (i.Children != null) foreach (var c in i.Children) Collect(c); }
            foreach (var m in menu) Collect(m);
            return emitted;
        }

        [Obsolete("Parity shadow log removed. No-op kept for interface compatibility.")]
        public Task LogParityIfShadowAsync(Guid tenantId, int? userId, string? userRole, HashSet<string> codeTreeIds, HashSet<string> tablesIds)
            => Task.CompletedTask;

        public async Task<List<DynamicMenuItemDto>> BuildMenuFromTablesAsync(
            HashSet<string> effectiveKeys,
            TenantMenuEntitlementsDto entitlements)
        {
            var effective = effectiveKeys ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool wildcard = effective.Contains("*");
            var tenantId = entitlements?.TenantId ?? Guid.Empty;

            var rows = await _db.MenuItems.AsNoTracking().Where(m => m.IsActive).OrderBy(m => m.DisplayOrder).ToListAsync();
            var keyById = rows.ToDictionary(r => r.Id, r => r.Key);
            var childrenByParentId = rows.Where(r => r.ParentId.HasValue)
                .GroupBy(r => r.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.DisplayOrder).ToList());

            // --- tenant ceiling: enabled modules + active page whitelist ---
            var enabledModuleIds = new HashSet<int>(await _db.TenantModules.IgnoreQueryFilters()
                .Where(tm => tm.TenantId == tenantId && tm.EnabledUntil == null)
                .Select(tm => tm.ModuleId)
                .ToListAsync());

            var activePageRows = await _db.TenantModulePages.IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId && p.EnabledUntil == null)
                .Select(p => new { p.ModuleId, p.PageKey })
                .ToListAsync();
            var pageWhitelistByModule = activePageRows
                .GroupBy(p => p.ModuleId)
                .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.PageKey), StringComparer.OrdinalIgnoreCase));

            // --- effective permission → (module, page) sets ---
            var effectiveModuleIds = new HashSet<int>();
            var effectivePageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!wildcard && effective.Count > 0)
            {
                var perms = await _db.Permissions.IgnoreQueryFilters()
                    .Select(p => new { p.Key, p.ModuleId, p.PageKey })
                    .ToListAsync();
                foreach (var p in perms)
                {
                    if (string.IsNullOrWhiteSpace(p.Key) || !effective.Contains(p.Key)) continue;
                    effectiveModuleIds.Add(p.ModuleId);
                    if (!string.IsNullOrEmpty(p.PageKey)) effectivePageKeys.Add(p.PageKey);
                }
            }

            bool PageEntitled(MenuItem mi)
            {
                if (!pageWhitelistByModule.TryGetValue(mi.ModuleId, out var whitelist)) return true; // no restriction
                return whitelist.Contains(mi.Key);
            }

            bool IsVisible(MenuItem mi)
            {
                if (wildcard) return true;
                if (!enabledModuleIds.Contains(mi.ModuleId)) return false;
                if (!PageEntitled(mi)) return false;
                if (effectivePageKeys.Contains(mi.Key)) return true;          // page-scoped grant
                if (!mi.ParentId.HasValue && effectiveModuleIds.Contains(mi.ModuleId)) return true; // module root
                return false;
            }

            var reportsEnabled = (entitlements?.Reports ?? new List<ReportEntitlementItemDto>())
                .Where(r => r.IsEnabled).ToList();

            var result = new List<DynamicMenuItemDto>();

            foreach (var top in rows.Where(r => !r.ParentId.HasValue).OrderBy(r => r.DisplayOrder))
            {
                bool isGroup = string.IsNullOrWhiteSpace(top.Path);

                var dto = new DynamicMenuItemDto
                {
                    Id = top.Key,
                    Title = top.Title,
                    Path = top.Path,
                    Icon = top.Icon,
                    PermissionKey = top.PermissionKey,
                    Badge = top.Badge,
                    Children = new List<DynamicMenuItemDto>()
                };

                if (childrenByParentId.TryGetValue(top.Id, out var children))
                {
                    foreach (var c in children)
                    {
                        if (!IsVisible(c)) continue;
                        dto.Children.Add(new DynamicMenuItemDto
                        {
                            Id = c.Key,
                            Title = c.Title,
                            Path = c.Path,
                            Icon = c.Icon,
                            PermissionKey = c.PermissionKey,
                            Badge = c.Badge,
                            Children = new List<DynamicMenuItemDto>()
                        });
                    }
                }

                // Reports children (visibility_rule = report_entitlement).
                if (string.Equals(top.VisibilityRule, "report_entitlement", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var rep in reportsEnabled)
                    {
                        var reportKey = "reports." + rep.ReportKey;
                        bool allowed = wildcard
                                       || effective.Contains(reportKey)
                                       || effective.Contains(reportKey + ".view")
                                       || effective.Contains("reports.view");
                        if (!allowed) continue;
                        dto.Children.Add(new DynamicMenuItemDto
                        {
                            Id = reportKey,
                            Title = rep.Title,
                            Path = "/reports?tab=" + rep.ReportKey,
                            Icon = "fileText",
                            PermissionKey = reportKey,
                            Children = new List<DynamicMenuItemDto>()
                        });
                    }
                    if (dto.Children.Count > 0) dto.Badge = dto.Children.Count.ToString();
                }

                if (isGroup)
                {
                    if (dto.Children.Count == 0 && !IsVisible(top)) continue;
                }
                else
                {
                    if (!IsVisible(top)) continue;
                }

                result.Add(dto);
            }

            return result;
        }

        private static MenuItemDto ToDto(MenuItem m, IReadOnlyDictionary<int, string> keyById) => new()
        {
            Id = m.Id,
            Key = m.Key,
            ParentKey = m.ParentId.HasValue && keyById.TryGetValue(m.ParentId.Value, out var pk) ? pk : null,
            Title = m.Title,
            Path = m.Path,
            Icon = m.Icon,
            PermissionKey = m.PermissionKey,
            Badge = m.Badge,
            DisplayOrder = m.DisplayOrder,
            VisibilityRule = m.VisibilityRule,
            IsActive = m.IsActive
        };
    }
}
