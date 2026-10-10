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
    /// TASK-049 Option B: menu rendering is a structural filter — a row is
    /// visible iff the user's effective permission set contains its
    /// PermissionKey (or its row.Key, as a fallback for parent-group rows
    /// whose permission_key is a .module placeholder not present in the
    /// permissions catalog). The TASK-048 extended IsVisible matcher
    /// (dotted-alias, NormalizeFeatureKey, .view fallback) is DELETED — the
    /// permissions catalog is now authoritative and permission keys are the
    /// identifiers downstream code expects.
    /// </summary>
    public class MenuCatalogService : IMenuCatalogService
    {
        private readonly KTransportDbContext _db;
        private readonly ILogger<MenuCatalogService> _log;

        public MenuCatalogService(
            KTransportDbContext db,
            ILogger<MenuCatalogService> log)
        {
            _db = db;
            _log = log;
        }

        public async Task<List<MenuItemDto>> GetAllAsync()
        {
            var rows = await _db.MenuItems
                .AsNoTracking()
                .OrderBy(m => m.ParentKey)
                .ThenBy(m => m.DisplayOrder)
                .ToListAsync();

            return rows.Select(ToDto).ToList();
        }

        [Obsolete("TASK-049 Option B deleted the shadow-read dual-matcher. Kept as a thin wrapper for one release in case any dev script invokes it.")]
        public async Task<HashSet<string>> BuildMenuIdSetFromTablesAsync(
            Guid tenantId,
            int? userId,
            string? userRole,
            IReadOnlyCollection<string> effectivePermissionKeys)
        {
            var entitlements = new TenantMenuEntitlementsDto { TenantId = tenantId, Reports = new() };
            var effective = new HashSet<string>(effectivePermissionKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var menu = await BuildMenuFromTablesAsync(effective, entitlements);
            var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void collect(DynamicMenuItemDto i)
            {
                emitted.Add(i.Id);
                if (i.Children != null) foreach (var c in i.Children) collect(c);
            }
            foreach (var m in menu) collect(m);
            return emitted;
        }

        [Obsolete("TASK-049 Option B deleted the parity shadow log. Kept as a no-op for one release.")]
        public Task LogParityIfShadowAsync(
            Guid tenantId,
            int? userId,
            string? userRole,
            HashSet<string> codeTreeIds,
            HashSet<string> tablesIds) => Task.CompletedTask;

        /// <summary>
        /// TASK-049 Option B: the sole production menu builder.
        ///
        /// Visibility rule (per menu.md §5.4):
        ///   - SUPER_USER wildcard '*': every active menu_items row.
        ///   - Else a leaf is visible iff <c>effective.Contains(row.PermissionKey)</c>
        ///     OR <c>effective.Contains(row.Key)</c> (back-compat for the
        ///     legacy NavigationService(ctx) ctor path that still passes
        ///     feature keys instead of permission keys).
        ///   - A parent-group row (no path) is emitted iff any of its
        ///     children are visible.
        ///   - The <c>reports</c> parent appends per-tenant children from
        ///     <c>entitlements.Reports</c> (visibility_rule='report_entitlement').
        /// </summary>
        public async Task<List<DynamicMenuItemDto>> BuildMenuFromTablesAsync(
            HashSet<string> effectiveKeys,
            TenantMenuEntitlementsDto entitlements)
        {
            var effective = effectiveKeys ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rows = await _db.MenuItems
                .AsNoTracking()
                .Where(m => m.IsActive)
                .OrderBy(m => m.ParentKey)
                .ThenBy(m => m.DisplayOrder)
                .ToListAsync();

            var byParent = rows
                .Where(r => r.ParentKey != null)
                .GroupBy(r => r.ParentKey!)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.DisplayOrder).ToList(), StringComparer.OrdinalIgnoreCase);

            bool wildcard = effective.Contains("*");

            // Structural filter — no fuzzy matching, no dotted-alias, no
            // NormalizeFeatureKey. The authoritative identifiers are
            // menu_items.key and menu_items.permission_key.
            bool IsVisible(MenuItem row)
            {
                if (wildcard) return true;
                if (string.IsNullOrWhiteSpace(row.PermissionKey)) return true;

                if (effective.Contains(row.Key)) return true;
                if (effective.Contains(row.PermissionKey)) return true;
                return false;
            }

            var reportsByKey = (entitlements?.Reports ?? new List<ReportEntitlementItemDto>())
                .Where(r => r.IsEnabled)
                .ToList();

            var result = new List<DynamicMenuItemDto>();

            foreach (var top in rows.Where(r => r.ParentKey == null).OrderBy(r => r.DisplayOrder))
            {
                bool isParentGroup = string.IsNullOrWhiteSpace(top.Path);

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

                if (byParent.TryGetValue(top.Key, out var children))
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

                // Reports children: visibility_rule='report_entitlement'.
                if (string.Equals(top.VisibilityRule, "report_entitlement", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var rep in reportsByKey)
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
                    if (dto.Children.Count > 0)
                    {
                        dto.Badge = dto.Children.Count.ToString();
                    }
                }

                if (isParentGroup)
                {
                    // Parent group needs at least one visible child.
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

        private static MenuItemDto ToDto(MenuItem m) => new()
        {
            Id = m.Id,
            Key = m.Key,
            ParentKey = m.ParentKey,
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
