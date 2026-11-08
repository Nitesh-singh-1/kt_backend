using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Controllers
{
    /// <summary>
    /// TASK-040 Phase 1 admin surface — platform-admin only.
    /// GET /api/admin/entitlements/parity-report — last 24h of mismatches by tenant.
    /// POST /api/admin/entitlements/backfill   — idempotent full backfill.
    /// </summary>
    [ApiController]
    [Route("api/admin/entitlements")]
    [Authorize]
    [RequirePlatformAdmin]
    public class EntitlementsAdminController : ControllerBase
    {
        private readonly KTransportDbContext _db;
        private readonly IEntitlementsService _entitlements;

        public EntitlementsAdminController(KTransportDbContext db, IEntitlementsService entitlements)
        {
            _db = db;
            _entitlements = entitlements;
        }

        [HttpGet("parity-report")]
        public async Task<ActionResult<ParityReportDto>> GetParityReport()
        {
            var since = DateTime.UtcNow.AddHours(-24);
            var rows = await _db.PermissionParityLogs
                .Where(l => l.LoggedAt >= since)
                .ToListAsync();

            var grouped = rows
                .GroupBy(r => r.TenantId)
                .Select(g => new ParityReportTenantGroupDto
                {
                    TenantId = g.Key,
                    MismatchCount = g.Count(),
                    LastMismatchAt = g.Max(r => r.LoggedAt),
                    SampleJsonOnlyKeys = g.OrderByDescending(r => r.LoggedAt).Take(5)
                        .Select(r => r.JsonOnlyKeys ?? string.Empty).ToList(),
                    SampleTablesOnlyKeys = g.OrderByDescending(r => r.LoggedAt).Take(5)
                        .Select(r => r.TablesOnlyKeys ?? string.Empty).ToList()
                })
                .OrderByDescending(x => x.MismatchCount)
                .ToList();

            return Ok(new ParityReportDto
            {
                GeneratedAt = DateTime.UtcNow,
                WindowStart = since,
                TotalMismatches = rows.Count,
                ByTenant = grouped
            });
        }

        [HttpPost("backfill")]
        [Obsolete("TASK-044 Phase 3: no JSON source to backfill FROM. Returns 410 Gone.")]
        public ActionResult RunBackfill()
        {
            return StatusCode(410, new { message = "Backfill not available in Phase 3. Use PUT /api/configuration/menu-entitlements for new grants." });
        }

        /// <summary>
        /// Phase 1 keystone follow-up — the authoritative module catalog for the
        /// platform-admin /clients assign/revoke UI. Served from the live
        /// <c>modules</c> table so the frontend can never silently omit a real
        /// module (e.g. the paid <c>accounting</c> add-on). Replaces the
        /// hardcoded <c>ALL_MODULE_CODES</c> list in the frontend
        /// <c>moduleCatalog.ts</c>.
        /// </summary>
        [HttpGet("/api/admin/modules")]
        public async Task<ActionResult<List<ModuleCatalogItemDto>>> GetModuleCatalog()
        {
            var modules = await _db.Modules
                .IgnoreQueryFilters()
                .Where(m => m.IsActive)
                .OrderBy(m => m.DisplayOrder)
                .ThenBy(m => m.Code)
                .Select(m => new ModuleCatalogItemDto
                {
                    Code = m.Code,
                    Name = m.Name,
                    Description = m.Description,
                    IsPremium = m.IsPremium,
                    DisplayOrder = m.DisplayOrder
                })
                .ToListAsync();

            return Ok(modules);
        }

        /// <summary>§A: the navigable pages of a module (menu_items), for the page-level entitlement picker.</summary>
        [HttpGet("/api/admin/modules/{code}/pages")]
        public async Task<ActionResult<List<PageCatalogItemDto>>> GetModulePages(string code)
        {
            var moduleId = await _db.Modules.IgnoreQueryFilters()
                .Where(m => m.Code.ToLower() == code.ToLower())
                .Select(m => (int?)m.Id).FirstOrDefaultAsync();
            if (moduleId == null) return NotFound(new { success = false, message = $"Unknown module '{code}'." });

            var rows = await _db.MenuItems.IgnoreQueryFilters()
                .Where(m => m.ModuleId == moduleId.Value && m.IsActive)
                .OrderBy(m => m.DisplayOrder).ToListAsync();
            var keyById = await _db.MenuItems.IgnoreQueryFilters().ToDictionaryAsync(m => m.Id, m => m.Key);

            var pages = rows.Select(m => new PageCatalogItemDto
            {
                PageKey = m.Key,
                Title = m.Title,
                Path = m.Path,
                ParentKey = m.ParentId.HasValue && keyById.TryGetValue(m.ParentId.Value, out var pk) ? pk : null,
                DisplayOrder = m.DisplayOrder
            }).ToList();
            return Ok(pages);
        }

        /// <summary>§A: convenience — the full module catalog with each module's pages.</summary>
        [HttpGet("/api/admin/catalog")]
        public async Task<ActionResult<List<ModuleWithPagesDto>>> GetCatalog()
        {
            var modules = await _db.Modules.IgnoreQueryFilters().Where(m => m.IsActive)
                .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Code).ToListAsync();
            var menuItems = await _db.MenuItems.IgnoreQueryFilters().Where(m => m.IsActive).OrderBy(m => m.DisplayOrder).ToListAsync();
            var keyById = menuItems.ToDictionary(m => m.Id, m => m.Key);
            var pagesByModule = menuItems.GroupBy(m => m.ModuleId).ToDictionary(g => g.Key, g => g.ToList());

            var result = modules.Select(m => new ModuleWithPagesDto
            {
                Code = m.Code,
                Name = m.Name,
                IsPremium = m.IsPremium,
                DisplayOrder = m.DisplayOrder,
                Pages = (pagesByModule.TryGetValue(m.Id, out var mi) ? mi : new List<Models.MenuItem>())
                    .Select(x => new PageCatalogItemDto
                    {
                        PageKey = x.Key,
                        Title = x.Title,
                        Path = x.Path,
                        ParentKey = x.ParentId.HasValue && keyById.TryGetValue(x.ParentId.Value, out var pk) ? pk : null,
                        DisplayOrder = x.DisplayOrder
                    }).ToList()
            }).ToList();
            return Ok(result);
        }
    }
}
