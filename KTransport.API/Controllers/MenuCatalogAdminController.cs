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
    /// TASK-041 Phase 1 admin surface — platform-admin only.
    /// GET /api/admin/menu-items              — full catalog (read-only in Phase 1).
    /// GET /api/admin/menu-items/parity-report — last 24h of dual-read mismatches by tenant.
    /// </summary>
    [ApiController]
    [Route("api/admin/menu-items")]
    [Authorize]
    [RequirePlatformAdmin]
    public class MenuCatalogAdminController : ControllerBase
    {
        private readonly KTransportDbContext _db;
        private readonly IMenuCatalogService _menu;

        public MenuCatalogAdminController(KTransportDbContext db, IMenuCatalogService menu)
        {
            _db = db;
            _menu = menu;
        }

        [HttpGet]
        public async Task<ActionResult<List<MenuItemDto>>> GetAll()
        {
            var rows = await _menu.GetAllAsync();
            return Ok(rows);
        }

        [HttpGet("parity-report")]
        public async Task<ActionResult<MenuParityReportDto>> GetParityReport()
        {
            var since = DateTime.UtcNow.AddHours(-24);
            var rows = await _db.MenuParityLogs
                .Where(l => l.LoggedAt >= since)
                .ToListAsync();

            var grouped = rows
                .GroupBy(r => r.TenantId)
                .Select(g => new MenuParityReportTenantGroupDto
                {
                    TenantId = g.Key,
                    MismatchCount = g.Count(),
                    LastMismatchAt = g.Max(r => r.LoggedAt),
                    SampleCodeOnlyKeys = g.OrderByDescending(r => r.LoggedAt).Take(5)
                        .Select(r => r.CodeOnlyKeys ?? new List<string>()).ToList(),
                    SampleTablesOnlyKeys = g.OrderByDescending(r => r.LoggedAt).Take(5)
                        .Select(r => r.TablesOnlyKeys ?? new List<string>()).ToList()
                })
                .OrderByDescending(x => x.MismatchCount)
                .ToList();

            return Ok(new MenuParityReportDto
            {
                GeneratedAt = DateTime.UtcNow,
                WindowStart = since,
                TotalMismatches = rows.Count,
                ByTenant = grouped
            });
        }

        [HttpPatch("{id:int}/status")]
        [HttpPut("{id:int}/status")]
        public async Task<ActionResult<MenuItemDto>> UpdateStatus(int id, [FromBody] UpdateMenuItemStatusRequest request)
        {
            var item = await _db.MenuItems.FindAsync(id);
            if (item == null)
            {
                return NotFound(new { success = false, message = $"Menu item with ID {id} not found." });
            }

            item.IsActive = request.IsActive;
            await _db.SaveChangesAsync();

            return Ok(ToDto(item));
        }

        [HttpPost("batch-status")]
        public async Task<ActionResult> BatchUpdateStatus([FromBody] BatchUpdateMenuItemStatusRequest request)
        {
            IQueryable<Models.MenuItem> query = _db.MenuItems;

            if (request.Ids != null && request.Ids.Count > 0)
            {
                query = query.Where(m => request.Ids.Contains(m.Id));
            }
            else if (request.Keys != null && request.Keys.Count > 0)
            {
                query = query.Where(m => request.Keys.Contains(m.Key));
            }
            else
            {
                return BadRequest(new { success = false, message = "Either 'ids' or 'keys' must be provided." });
            }

            var items = await query.ToListAsync();
            foreach (var item in items)
            {
                item.IsActive = request.IsActive;
            }

            await _db.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                updatedCount = items.Count,
                updatedKeys = items.Select(i => i.Key).ToList(),
                isActive = request.IsActive
            });
        }

        [HttpPost("hide-incomplete-master-data")]
        public async Task<ActionResult> HideIncompleteMasterData()
        {
            // Keep Party Directory and Fleet & Stations active; deactivate the other 8 incomplete master data items
            var allowedKeys = new[] { "master_data.parties", "master_data.fleet" };
            var items = await _db.MenuItems
                .Where(m => m.ParentKey == "master_data" && !allowedKeys.Contains(m.Key))
                .ToListAsync();

            foreach (var item in items)
            {
                item.IsActive = false;
            }

            await _db.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                message = "Incomplete master data menus have been hidden.",
                hiddenKeys = items.Select(i => i.Key).ToList()
            });
        }

        [HttpPost("reset-master-data")]
        public async Task<ActionResult> ResetMasterData()
        {
            var items = await _db.MenuItems
                .Where(m => m.ParentKey == "master_data")
                .ToListAsync();

            foreach (var item in items)
            {
                item.IsActive = true;
            }

            await _db.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                message = "All master data menus have been restored as active.",
                activeKeys = items.Select(i => i.Key).ToList()
            });
        }

        private static MenuItemDto ToDto(Models.MenuItem m) => new()
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
