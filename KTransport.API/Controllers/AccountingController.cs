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
    /// Accounting &amp; Finance Phase 1 (TASK-016). Chart of accounts, financial periods, the
    /// double-entry voucher engine, and read reports (Day Book / Ledger / Trial Balance).
    /// Gated by the paid ACCOUNTING module, then per-area permission keys.
    /// </summary>
    [ApiController]
    [Route("api/accounting")]
    [Authorize]
    [RequireFeature(FeatureConstants.ACCOUNTING)]
    public class AccountingController : ControllerBase
    {
        private readonly IAccountService _accounts;
        private readonly IFinancialPeriodService _periods;
        private readonly IVoucherService _vouchers;
        private readonly IAccountingReportService _reports;

        public AccountingController(
            IAccountService accounts,
            IFinancialPeriodService periods,
            IVoucherService vouchers,
            IAccountingReportService reports)
        {
            _accounts = accounts;
            _periods = periods;
            _vouchers = vouchers;
            _reports = reports;
        }

        private int? GetCurrentUserId()
        {
            var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            return int.TryParse(raw, out var id) ? id : (int?)null;
        }

        private ObjectResult Problem(AccountingException ex) =>
            StatusCode(ex.StatusCode, new { success = false, message = ex.Message });

        // ---------------- Accounts ----------------

        [HttpGet("accounts")]
        [RequirePermission("accounting.accounts.view")]
        public async Task<ActionResult<List<AccountDto>>> GetAccounts(
            [FromQuery] string? search = null, [FromQuery] string? type = null, [FromQuery] bool includeInactive = false)
            => Ok(await _accounts.GetAccountsAsync(search, type, includeInactive));

        [HttpGet("accounts/tree")]
        [RequirePermission("accounting.accounts.view")]
        public async Task<ActionResult<List<AccountNodeDto>>> GetAccountTree()
            => Ok(await _accounts.GetTreeAsync());

        [HttpGet("accounts/{id:long}")]
        [RequirePermission("accounting.accounts.view")]
        public async Task<ActionResult<AccountDto>> GetAccount(long id)
        {
            var a = await _accounts.GetByIdAsync(id);
            return a == null ? NotFound() : Ok(a);
        }

        [HttpPost("accounts")]
        [RequirePermission("accounting.accounts.create")]
        public async Task<ActionResult<AccountDto>> CreateAccount([FromBody] CreateAccountRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try { return Ok(await _accounts.CreateAsync(request)); }
            catch (AccountingException ex) { return Problem(ex); }
        }

        [HttpPut("accounts/{id:long}")]
        [RequirePermission("accounting.accounts.edit")]
        public async Task<ActionResult<AccountDto>> UpdateAccount(long id, [FromBody] UpdateAccountRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var a = await _accounts.UpdateAsync(id, request);
                return a == null ? NotFound() : Ok(a);
            }
            catch (AccountingException ex) { return Problem(ex); }
        }

        [HttpDelete("accounts/{id:long}")]
        [RequirePermission("accounting.accounts.delete")]
        public async Task<ActionResult> DeleteAccount(long id)
        {
            try
            {
                var ok = await _accounts.DeleteAsync(id);
                return ok ? NoContent() : NotFound();
            }
            catch (AccountingException ex) { return Problem(ex); }
        }

        // ---------------- Periods ----------------

        [HttpGet("periods")]
        [RequirePermission("accounting.periods.view")]
        public async Task<ActionResult<List<FinancialPeriodDto>>> GetPeriods()
            => Ok(await _periods.GetPeriodsAsync());

        [HttpPost("periods")]
        [RequirePermission("accounting.periods.create")]
        public async Task<ActionResult<FinancialPeriodDto>> CreatePeriod([FromBody] CreatePeriodRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try { return Ok(await _periods.CreateAsync(request)); }
            catch (AccountingException ex) { return Problem(ex); }
        }

        [HttpPost("periods/{id:long}/close")]
        [RequirePermission("accounting.periods.edit")]
        public async Task<ActionResult<FinancialPeriodDto>> ClosePeriod(long id)
        {
            var p = await _periods.CloseAsync(id);
            return p == null ? NotFound() : Ok(p);
        }

        [HttpPost("periods/{id:long}/reopen")]
        [RequirePermission("accounting.periods.edit")]
        public async Task<ActionResult<FinancialPeriodDto>> ReopenPeriod(long id)
        {
            var p = await _periods.ReopenAsync(id);
            return p == null ? NotFound() : Ok(p);
        }

        // ---------------- Vouchers ----------------

        [HttpGet("vouchers")]
        [RequirePermission("accounting.vouchers.view")]
        public async Task<ActionResult<VoucherListResponse>> GetVouchers(
            [FromQuery] string? type = null, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
            [FromQuery] long? accountId = null, [FromQuery] string? search = null,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
            => Ok(await _vouchers.GetVouchersAsync(type, from, to, accountId, search, page, pageSize));

        [HttpGet("vouchers/{id:long}")]
        [RequirePermission("accounting.vouchers.view")]
        public async Task<ActionResult<VoucherDto>> GetVoucher(long id)
        {
            var v = await _vouchers.GetByIdAsync(id);
            return v == null ? NotFound() : Ok(v);
        }

        [HttpPost("vouchers")]
        [RequirePermission("accounting.vouchers.create")]
        public async Task<ActionResult<VoucherDto>> CreateVoucher([FromBody] CreateVoucherRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try { return Ok(await _vouchers.CreateAsync(request, GetCurrentUserId())); }
            catch (AccountingException ex) { return Problem(ex); }
        }

        [HttpPost("vouchers/{id:long}/reverse")]
        [RequirePermission("accounting.vouchers.delete")]
        public async Task<ActionResult<VoucherDto>> ReverseVoucher(long id, [FromBody] ReverseVoucherRequest request)
        {
            try
            {
                var v = await _vouchers.ReverseAsync(id, request ?? new ReverseVoucherRequest(), GetCurrentUserId());
                return v == null ? NotFound() : Ok(v);
            }
            catch (AccountingException ex) { return Problem(ex); }
        }

        // ---------------- Reports ----------------

        [HttpGet("reports/day-book")]
        [RequirePermission("accounting.reports.view")]
        public async Task<ActionResult<List<DayBookEntryDto>>> GetDayBook([FromQuery] DateOnly from, [FromQuery] DateOnly to)
            => Ok(await _reports.GetDayBookAsync(from, to));

        [HttpGet("reports/ledger")]
        [RequirePermission("accounting.reports.view")]
        public async Task<ActionResult<AccountLedgerDto>> GetLedger([FromQuery] long accountId, [FromQuery] DateOnly from, [FromQuery] DateOnly to)
        {
            var l = await _reports.GetLedgerAsync(accountId, from, to);
            return l == null ? NotFound() : Ok(l);
        }

        [HttpGet("reports/trial-balance")]
        [RequirePermission("accounting.reports.view")]
        public async Task<ActionResult<TrialBalanceDto>> GetTrialBalance([FromQuery] DateOnly asOf)
            => Ok(await _reports.GetTrialBalanceAsync(asOf));
    }
}
