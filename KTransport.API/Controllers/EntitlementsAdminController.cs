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
    }
}
