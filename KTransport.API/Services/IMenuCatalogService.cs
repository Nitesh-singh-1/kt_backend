using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface IMenuCatalogService
    {
        /// <summary>Reads the full catalog, ordered by (parent_key, display_order).</summary>
        Task<List<MenuItemDto>> GetAllAsync();

        /// <summary>
        /// Phase 1/2 shadow helper. Phase 3 removed the dual-read harness.
        /// Kept behind <c>[Obsolete]</c> for one release in case any dev
        /// script still invokes it.
        /// </summary>
        [Obsolete("Phase 1/2 shadow helper. Phase 3 removed the dual-read harness. Kept for one release in case any dev script invokes it. Will be deleted in next release.")]
        Task<HashSet<string>> BuildMenuIdSetFromTablesAsync(
            Guid tenantId,
            int? userId,
            string? userRole,
            IReadOnlyCollection<string> effectivePermissionKeys);

        /// <summary>
        /// Phase 1/2 shadow helper. Phase 3 removed the dual-read harness.
        /// Kept behind <c>[Obsolete]</c> for one release in case any dev
        /// script still invokes it.
        /// </summary>
        [Obsolete("Phase 1/2 shadow helper. Phase 3 removed the dual-read harness. Kept for one release in case any dev script invokes it. Will be deleted in next release.")]
        Task LogParityIfShadowAsync(
            Guid tenantId,
            int? userId,
            string? userRole,
            HashSet<string> codeTreeIds,
            HashSet<string> tablesIds);

        /// <summary>
        /// TASK-045 Phase 3: sole production path. Loads <c>menu_items</c>
        /// rows, filters by <paramref name="effectiveKeys"/>, and appends
        /// per-tenant conditional children for the <c>reports</c> parent
        /// (<c>visibility_rule='report_entitlement'</c>). Returns a
        /// ready-to-serialize list of <see cref="DynamicMenuItemDto"/>.
        /// </summary>
        Task<List<DynamicMenuItemDto>> BuildMenuFromTablesAsync(
            HashSet<string> effectiveKeys,
            TenantMenuEntitlementsDto entitlements);
    }
}
