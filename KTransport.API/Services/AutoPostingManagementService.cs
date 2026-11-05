using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class AutoPostingManagementService : IAutoPostingManagementService
    {
        private readonly KTransportDbContext _context;
        private readonly ITenantContext _tenant;
        private readonly IVoucherService _vouchers;

        public AutoPostingManagementService(KTransportDbContext context, ITenantContext tenant, IVoucherService vouchers)
        {
            _context = context;
            _tenant = tenant;
            _vouchers = vouchers;
        }

        // ---------------- settings ----------------

        public async Task<AutoPostingSettingsDto> GetSettingsAsync()
        {
            var s = await _context.AccountingSettings.AsNoTracking().FirstOrDefaultAsync();
            return s == null
                ? new AutoPostingSettingsDto { TenantId = _tenant.CurrentTenantId, AutoPostingEnabled = false }
                : Map(s);
        }

        public async Task<AutoPostingSettingsDto> UpdateSettingsAsync(UpdateAutoPostingSettingsRequest request, int? userId)
        {
            var s = await _context.AccountingSettings.FirstOrDefaultAsync();
            var now = DateTime.UtcNow;
            if (s == null)
            {
                s = new AccountingSettings
                {
                    TenantId = _tenant.CurrentTenantId,
                    AutoPostingEnabled = request.AutoPostingEnabled,
                    DefaultBankAccountId = request.DefaultBankAccountId,
                    EnabledAt = request.AutoPostingEnabled ? now : null,
                    EnabledBy = request.AutoPostingEnabled ? userId : null,
                    CreatedAt = now
                };
                _context.AccountingSettings.Add(s);
            }
            else
            {
                // Stamp EnabledAt/EnabledBy on an OFF -> ON transition.
                if (request.AutoPostingEnabled && !s.AutoPostingEnabled)
                {
                    s.EnabledAt = now;
                    s.EnabledBy = userId;
                }
                s.AutoPostingEnabled = request.AutoPostingEnabled;
                s.DefaultBankAccountId = request.DefaultBankAccountId;
                s.UpdatedAt = now;
            }
            await _context.SaveChangesAsync();
            return Map(s);
        }

        private static AutoPostingSettingsDto Map(AccountingSettings s) => new()
        {
            TenantId = s.TenantId,
            AutoPostingEnabled = s.AutoPostingEnabled,
            DefaultBankAccountId = s.DefaultBankAccountId,
            EnabledAt = s.EnabledAt,
            EnabledBy = s.EnabledBy
        };

        // ---------------- queue ----------------

        public async Task<PagedResult<AutoPostingQueueItemDto>> GetQueueAsync(string? status, DateOnly? from, DateOnly? to, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 50;

            var query = _context.AutoPostingQueue.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AutoPostStatus>(status, true, out var st))
                query = query.Where(r => r.Status == st);
            if (from.HasValue)
            {
                var fromDt = from.Value.ToDateTime(TimeOnly.MinValue);
                query = query.Where(r => r.EnqueuedAt >= fromDt);
            }
            if (to.HasValue)
            {
                var toDt = to.Value.ToDateTime(TimeOnly.MaxValue);
                query = query.Where(r => r.EnqueuedAt <= toDt);
            }

            var total = await query.CountAsync();
            var rows = await query
                .OrderByDescending(r => r.EnqueuedAt).ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();

            // Resolve the current live voucher id per (sourceType, sourceId) on this page.
            var links = await ResolveLiveVoucherIdsAsync(rows.Select(r => (r.SourceType, r.SourceId)));

            var items = rows.Select(r => new AutoPostingQueueItemDto
            {
                Id = r.Id,
                SourceType = r.SourceType,
                SourceId = r.SourceId,
                Operation = r.Operation.ToString(),
                Status = r.Status.ToString(),
                Attempts = r.Attempts,
                LastError = r.LastError,
                EnqueuedAt = r.EnqueuedAt,
                ProcessedAt = r.ProcessedAt,
                LinkedVoucherId = links.TryGetValue((r.SourceType, r.SourceId), out var vid) ? vid : null
            }).ToList();

            return new PagedResult<AutoPostingQueueItemDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            };
        }

        public async Task<AutoPostingQueueItemDto?> RetryAsync(long id)
        {
            var row = await _context.AutoPostingQueue.FirstOrDefaultAsync(r => r.Id == id);
            if (row == null) return null;
            if (row.Status != AutoPostStatus.Failed && row.Status != AutoPostStatus.Deferred)
                throw AccountingException.Conflict("Only a Failed or Deferred queue row can be retried.");

            row.Status = AutoPostStatus.Pending;
            row.Attempts = 0;
            row.LastError = null;
            row.ProcessedAt = null;
            await _context.SaveChangesAsync();

            var linkedId = await LiveVoucherIdAsync(row.SourceType, row.SourceId);
            return new AutoPostingQueueItemDto
            {
                Id = row.Id,
                SourceType = row.SourceType,
                SourceId = row.SourceId,
                Operation = row.Operation.ToString(),
                Status = row.Status.ToString(),
                Attempts = row.Attempts,
                LastError = row.LastError,
                EnqueuedAt = row.EnqueuedAt,
                ProcessedAt = row.ProcessedAt,
                LinkedVoucherId = linkedId
            };
        }

        // ---------------- reconciliation ----------------

        public async Task<ReconciliationReportDto> GetReconciliationAsync(DateOnly? from, DateOnly? to)
        {
            var fromD = from ?? DateOnly.MinValue;
            var toD = to ?? DateOnly.MaxValue;

            // Check 1: freight billed (Invoice.SubTotal) == Freight Income credits.
            var billed = await _context.Invoices.AsNoTracking()
                .Where(i => i.IsActive && i.InvoiceDate >= fromD && i.InvoiceDate <= toD)
                .SumAsync(i => (decimal?)i.SubTotal) ?? 0m;

            var freightIncome = await _context.VoucherLines.AsNoTracking()
                .Where(l => l.DrCr == DrCr.CR
                    && l.Account!.Code == WellKnownLedgers.FreightIncome
                    && l.Voucher!.Status == VoucherStatus.Posted
                    && l.Voucher.Date >= fromD && l.Voucher.Date <= toD)
                .SumAsync(l => (decimal?)l.Amount) ?? 0m;

            // Check 2: receipts (settlements + invoice payments) == A/R credited on Receipt vouchers.
            var settlements = await _context.Shipments.AsNoTracking()
                .Where(s => s.IsActive && (s.DeliveryDate ?? s.ShipmentDate) >= fromD && (s.DeliveryDate ?? s.ShipmentDate) <= toD)
                .SumAsync(s => (decimal?)(s.SettledReceivedAmount ?? 0m)) ?? 0m;
            var invoicePayments = await _context.Invoices.AsNoTracking()
                .Where(i => i.IsActive && i.InvoiceDate >= fromD && i.InvoiceDate <= toD)
                .SumAsync(i => (decimal?)i.PaidAmount) ?? 0m;
            var receiptsExpected = settlements + invoicePayments;

            var debtorGroupId = await _context.Accounts.AsNoTracking()
                .Where(a => a.Code == "1160" && a.IsGroup).Select(a => (long?)a.Id).FirstOrDefaultAsync();

            var arCredited = debtorGroupId == null ? 0m : await _context.VoucherLines.AsNoTracking()
                .Where(l => l.DrCr == DrCr.CR
                    && l.Account!.ParentId == debtorGroupId
                    && l.Voucher!.VoucherType == VoucherType.Receipt
                    && l.Voucher.Status == VoucherStatus.Posted
                    && l.Voucher.Date >= fromD && l.Voucher.Date <= toD)
                .SumAsync(l => (decimal?)l.Amount) ?? 0m;

            var report = new ReconciliationReportDto
            {
                Checks = new List<ReconciliationCheckDto>
                {
                    MakeCheck("FreightBilledEqualsFreightIncome", billed, freightIncome),
                    MakeCheck("ReceiptsEqualArCredited", receiptsExpected, arCredited),
                },
                PendingCount = await _context.AutoPostingQueue.CountAsync(r => r.Status == AutoPostStatus.Pending),
                DeferredCount = await _context.AutoPostingQueue.CountAsync(r => r.Status == AutoPostStatus.Deferred),
                FailedCount = await _context.AutoPostingQueue.CountAsync(r => r.Status == AutoPostStatus.Failed),
            };
            return report;
        }

        private static ReconciliationCheckDto MakeCheck(string name, decimal expected, decimal actual)
        {
            var diff = Math.Round(expected - actual, 2);
            return new ReconciliationCheckDto
            {
                Name = name,
                Expected = Math.Round(expected, 2),
                Actual = Math.Round(actual, 2),
                Difference = diff,
                Ok = Math.Abs(diff) < 0.01m
            };
        }

        // ---------------- drill-through ----------------

        public async Task<List<VoucherDto>> GetVouchersBySourceAsync(string sourceType, long sourceId)
        {
            // The source voucher(s) + any reversals pointing at them, newest first.
            var sourceVoucherIds = await _context.Vouchers.AsNoTracking()
                .Where(v => v.SourceType == sourceType && v.SourceId == sourceId)
                .Select(v => v.Id)
                .ToListAsync();

            var ids = await _context.Vouchers.AsNoTracking()
                .Where(v => (v.SourceType == sourceType && v.SourceId == sourceId)
                    || (v.ReversalOfId != null && sourceVoucherIds.Contains(v.ReversalOfId.Value)))
                .OrderByDescending(v => v.Id)
                .Select(v => v.Id)
                .ToListAsync();

            var result = new List<VoucherDto>();
            foreach (var id in ids)
            {
                var dto = await _vouchers.GetByIdAsync(id);
                if (dto != null) result.Add(dto);
            }
            return result;
        }

        // ---------------- helpers ----------------

        private async Task<long?> LiveVoucherIdAsync(string sourceType, long sourceId)
            => await _context.Vouchers.AsNoTracking()
                .Where(v => v.SourceType == sourceType && v.SourceId == sourceId
                    && v.ReversalOfId == null && v.Status == VoucherStatus.Posted)
                .Select(v => (long?)v.Id)
                .FirstOrDefaultAsync();

        private async Task<Dictionary<(string, long), long?>> ResolveLiveVoucherIdsAsync(IEnumerable<(string SourceType, long SourceId)> keys)
        {
            var distinct = keys.Distinct().ToList();
            var map = new Dictionary<(string, long), long?>();
            if (distinct.Count == 0) return map;

            var types = distinct.Select(k => k.SourceType).Distinct().ToList();
            var ids = distinct.Select(k => k.SourceId).Distinct().ToList();

            var live = await _context.Vouchers.AsNoTracking()
                .Where(v => v.SourceType != null && v.SourceId != null
                    && v.ReversalOfId == null && v.Status == VoucherStatus.Posted
                    && types.Contains(v.SourceType) && ids.Contains(v.SourceId.Value))
                .Select(v => new { v.SourceType, v.SourceId, v.Id })
                .ToListAsync();

            foreach (var v in live)
                map[(v.SourceType!, v.SourceId!.Value)] = v.Id;
            return map;
        }
    }
}
