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
    }
}
