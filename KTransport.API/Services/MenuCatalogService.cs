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
    /// TASK-045 Phase 3: menu_items catalog is the sole production source for
    /// <see cref="NavigationService.GetDynamicMenuAsync"/>. The Phase 1/2 shadow
    /// helpers (<see cref="BuildMenuIdSetFromTablesAsync"/> +
    /// <see cref="LogParityIfShadowAsync"/>) are kept behind <c>[Obsolete]</c>
    /// for one release in case any dev script still invokes them.
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

        [Obsolete("Phase 1/2 shadow helper. Phase 3 removed the dual-read harness. Kept for one release in case any dev script invokes it. Will be deleted in next release.")]
        public async Task<HashSet<string>> BuildMenuIdSetFromTablesAsync(
            Guid tenantId,
            int? userId,
            string? userRole,
            IReadOnlyCollection<string> effectivePermissionKeys)
        {
            var effective = new HashSet<string>(
                effectivePermissionKeys ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            var rows = await _db.MenuItems
                .AsNoTracking()
                .Where(m => m.IsActive)
                .OrderBy(m => m.ParentKey)
                .ThenBy(m => m.DisplayOrder)
                .ToListAsync();

            var byParent = rows
                .Where(r => r.ParentKey != null)
                .GroupBy(r => r.ParentKey!)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool wildcardSet = effective.Contains("*");

            foreach (var top in rows.Where(r => r.ParentKey == null))
            {
                bool topVisible = wildcardSet
                                  || effective.Contains(top.Key)
                                  || (!string.IsNullOrWhiteSpace(top.PermissionKey) && effective.Contains(top.PermissionKey))
                                  || (!string.IsNullOrWhiteSpace(top.PermissionKey) && effective.Contains(top.PermissionKey + ".view"))
                                  || string.IsNullOrWhiteSpace(top.PermissionKey);

                var visibleChildren = new List<MenuItem>();
                if (byParent.TryGetValue(top.Key, out var children))
                {
                    foreach (var c in children)
                    {
                        bool childVisible = wildcardSet
                                            || effective.Contains(c.Key)
                                            || (!string.IsNullOrWhiteSpace(c.PermissionKey) && effective.Contains(c.PermissionKey))
                                            || (!string.IsNullOrWhiteSpace(c.PermissionKey) && effective.Contains(c.PermissionKey + ".view"))
                                            || string.IsNullOrWhiteSpace(c.PermissionKey);
                        if (childVisible) visibleChildren.Add(c);
                    }
                }

                bool isParentGroup = string.IsNullOrWhiteSpace(top.Path);
                if (isParentGroup)
                {
                    if (visibleChildren.Count == 0) continue;
                    if (!topVisible) continue;
                }
                else
                {
                    if (!topVisible) continue;
                }

                emitted.Add(top.Key);
                foreach (var c in visibleChildren) emitted.Add(c.Key);
            }

            return emitted;
        }

        [Obsolete("Phase 1/2 shadow helper. Phase 3 removed the dual-read harness. Kept for one release in case any dev script invokes it. Will be deleted in next release.")]
        public async Task LogParityIfShadowAsync(
            Guid tenantId,
            int? userId,
            string? userRole,
            HashSet<string> codeTreeIds,
            HashSet<string> tablesIds)
        {
            try
            {
                bool IsDeferredReportChild(string id) =>
                    id.StartsWith("reports.", StringComparison.OrdinalIgnoreCase);

                var codeOnly = codeTreeIds
                    .Except(tablesIds, StringComparer.OrdinalIgnoreCase)
                    .Where(id => !IsDeferredReportChild(id))
                    .ToList();
                var tablesOnly = tablesIds
                    .Except(codeTreeIds, StringComparer.OrdinalIgnoreCase)
                    .Where(id => !IsDeferredReportChild(id))
                    .ToList();

                if (codeOnly.Count == 0 && tablesOnly.Count == 0) return;

                _log.LogWarning(
                    "menu_parity_mismatch tenant={TenantId} user={UserId} role={Role} code_only=[{CodeOnlyKeys}] tables_only=[{TablesOnlyKeys}]",
                    tenantId, userId, userRole, string.Join(",", codeOnly), string.Join(",", tablesOnly));

                _db.MenuParityLogs.Add(new MenuParityLog
                {
                    TenantId = tenantId,
                    UserId = userId,
                    UserRole = userRole,
                    CodeOnlyKeys = codeOnly,
                    TablesOnlyKeys = tablesOnly,
                    LoggedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "menu_parity dual-read logging failed tenant={TenantId}", tenantId);
            }
        }

        /// <summary>
        /// TASK-045 Phase 3: sole production path. Table-authoritative menu
        /// builder. Loads <c>menu_items</c> rows, filters by
        /// <paramref name="effectiveKeys"/>, and appends per-tenant conditional
        /// children for the <c>reports</c> parent
        /// (<c>visibility_rule='report_entitlement'</c>). Returns a
        /// ready-to-serialize list of <see cref="DynamicMenuItemDto"/>.
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

            // Super-user wildcard: short-circuits every permission check.
            bool wildcard = effective.Contains("*");

            bool IsVisible(MenuItem row)
            {
                if (wildcard) return true;
                if (string.IsNullOrWhiteSpace(row.PermissionKey)) return true;
                return effective.Contains(row.PermissionKey)
                       || effective.Contains(row.PermissionKey + ".view")
                       || effective.Contains(row.Key);
            }

            var reportsByKey = (entitlements?.Reports ?? new List<ReportEntitlementItemDto>())
                .Where(r => r.IsEnabled)
                .ToList();

            var result = new List<DynamicMenuItemDto>();

            foreach (var top in rows.Where(r => r.ParentKey == null).OrderBy(r => r.DisplayOrder))
            {
                if (!IsVisible(top)) continue;

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

                // Reports children: visibility_rule='report_entitlement' appends
                // dynamic children from entitlements.Reports (enabled only, with
                // per-report permission gating via effective keys).
                if (string.Equals(top.VisibilityRule, "report_entitlement", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var rep in reportsByKey)
                    {
                        var reportKey = "reports." + rep.ReportKey;
                        bool allowed = effective.Contains(reportKey)
                                       || effective.Contains(reportKey + ".view");
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

                // Parent groups (no path) are only emitted when they have at
                // least one visible child.
                bool isParentGroup = string.IsNullOrWhiteSpace(top.Path);
                if (isParentGroup && dto.Children.Count == 0) continue;

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
