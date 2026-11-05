using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.Common;
using KTransport.API.DTOs;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers
{
    /// <summary>
    /// Accounting &amp; Finance Phase 2 (auto-posting). The on/off settings, the outbox queue
    /// (list + retry), the reconciliation report, and the source -&gt; voucher drill-through.
    /// Gated by the paid ACCOUNTING module, then per-endpoint permission keys
    /// (accounting.autoposting.{view,manage} / accounting.reports.view / accounting.vouchers.view).
    /// </summary>
    [ApiController]
    [Route("api/accounting")]
    [Authorize]
    [RequireFeature(FeatureConstants.ACCOUNTING)]
    public class AutoPostingController : ControllerBase
    {
        private readonly IAutoPostingManagementService _mgmt;

        public AutoPostingController(IAutoPostingManagementService mgmt)
        {
            _mgmt = mgmt;
        }

        private int? GetCurrentUserId()
        {
            var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            return int.TryParse(raw, out var id) ? id : (int?)null;
        }

        private ObjectResult Problem(AccountingException ex) =>
            StatusCode(ex.StatusCode, new { success = false, message = ex.Message });

        // ---------------- settings ----------------

        [HttpGet("auto-posting/settings")]
        [RequirePermission("accounting.autoposting.view")]
        public async Task<ActionResult<AutoPostingSettingsDto>> GetSettings()
            => Ok(await _mgmt.GetSettingsAsync());

        [HttpPut("auto-posting/settings")]
        [RequirePermission("accounting.autoposting.manage")]
        public async Task<ActionResult<AutoPostingSettingsDto>> UpdateSettings([FromBody] UpdateAutoPostingSettingsRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _mgmt.UpdateSettingsAsync(request, GetCurrentUserId()));
        }

        // ---------------- queue ----------------

        [HttpGet("auto-posting/queue")]
        [RequirePermission("accounting.autoposting.view")]
        public async Task<ActionResult<PagedResult<AutoPostingQueueItemDto>>> GetQueue(
            [FromQuery] string? status = null, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
            => Ok(await _mgmt.GetQueueAsync(status, from, to, page, pageSize));

        [HttpPost("auto-posting/queue/{id:long}/retry")]
        [RequirePermission("accounting.autoposting.manage")]
        public async Task<ActionResult<AutoPostingQueueItemDto>> RetryQueueRow(long id)
        {
            try
            {
                var dto = await _mgmt.RetryAsync(id);
                return dto == null ? NotFound(new { success = false, message = $"Queue row {id} not found." }) : Ok(dto);
            }
            catch (AccountingException ex) { return Problem(ex); }
        }

        // ---------------- reconciliation ----------------

        [HttpGet("reconciliation")]
        [RequirePermission("accounting.reports.view")]
        public async Task<ActionResult<ReconciliationReportDto>> GetReconciliation(
            [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null)
            => Ok(await _mgmt.GetReconciliationAsync(from, to));

        // ---------------- drill-through ----------------

        [HttpGet("vouchers/by-source/{sourceType}/{sourceId:long}")]
        [RequirePermission("accounting.vouchers.view")]
        public async Task<ActionResult<List<VoucherDto>>> GetVouchersBySource(string sourceType, long sourceId)
            => Ok(await _mgmt.GetVouchersBySourceAsync(sourceType, sourceId));
    }
}
