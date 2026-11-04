using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KTransport.API.Services
{
    public class VoucherService : IVoucherService
    {
        private readonly KTransportDbContext _context;
        private readonly IFinancialPeriodService _periods;
        private readonly ILogger<VoucherService> _logger;

        public VoucherService(KTransportDbContext context, IFinancialPeriodService periods, ILogger<VoucherService> logger)
        {
            _context = context;
            _periods = periods;
            _logger = logger;
        }

        private static readonly Dictionary<VoucherType, string> Prefixes = new()
        {
            [VoucherType.Contra] = "CV",
            [VoucherType.Payment] = "PMT",
            [VoucherType.Receipt] = "RCT",
            [VoucherType.Journal] = "JV",
            [VoucherType.Sales] = "SV",
            [VoucherType.Purchase] = "PV",
            [VoucherType.CreditNote] = "CN",
            [VoucherType.DebitNote] = "DN",
        };

        public async Task<VoucherListResponse> GetVouchersAsync(string? type, DateOnly? from, DateOnly? to, long? accountId, string? search, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 50;

            var query = _context.Vouchers.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<VoucherType>(type, true, out var vt))
                query = query.Where(v => v.VoucherType == vt);
            if (from.HasValue) query = query.Where(v => v.Date >= from.Value);
            if (to.HasValue) query = query.Where(v => v.Date <= to.Value);
            if (accountId.HasValue)
                query = query.Where(v => v.Lines.Any(l => l.AccountId == accountId.Value));
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(v => v.VoucherNo.ToLower().Contains(s) || (v.Narration != null && v.Narration.ToLower().Contains(s)));
            }

            var total = await query.CountAsync();

            var rows = await query
                .OrderByDescending(v => v.Date).ThenByDescending(v => v.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(v => new
                {
                    v.Id, v.VoucherType, v.VoucherNo, v.Date, v.Narration, v.Status,
                    Amount = v.Lines.Where(l => l.DrCr == DrCr.DR).Sum(l => (decimal?)l.Amount) ?? 0m
                })
                .ToListAsync();

            return new VoucherListResponse
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                Items = rows.Select(r => new VoucherListItemDto
                {
                    Id = r.Id, VoucherType = r.VoucherType.ToString(), VoucherNo = r.VoucherNo,
                    Date = r.Date, Narration = r.Narration, Status = r.Status.ToString(), Amount = r.Amount
                }).ToList()
            };
        }

        public async Task<VoucherDto?> GetByIdAsync(long id)
        {
            var v = await _context.Vouchers.AsNoTracking()
                .Include(x => x.Lines).ThenInclude(l => l.Account)
                .FirstOrDefaultAsync(x => x.Id == id);
            return v == null ? null : MapDetail(v);
        }

        public async Task<VoucherDto> CreateAsync(CreateVoucherRequest request, int? userId)
        {
            var lines = request.Lines ?? new List<VoucherLineInput>();
            if (lines.Count < 2)
                throw AccountingException.Unprocessable("A voucher needs at least two lines.");
            if (lines.Any(l => l.Amount <= 0))
                throw AccountingException.Unprocessable("Every line amount must be greater than zero.");

            var totalDr = Math.Round(lines.Where(l => l.DrCr == DrCr.DR).Sum(l => l.Amount), 2);
            var totalCr = Math.Round(lines.Where(l => l.DrCr == DrCr.CR).Sum(l => l.Amount), 2);
            if (totalDr != totalCr)
                throw AccountingException.Unprocessable($"Voucher is not balanced: debits {totalDr:0.00} ≠ credits {totalCr:0.00}.");
            if (totalDr == 0)
                throw AccountingException.Unprocessable("Voucher total cannot be zero.");

            // Accounts must exist, be active, and be postable ledgers (not groups).
            var accountIds = lines.Select(l => l.AccountId).Distinct().ToList();
            var accounts = await _context.Accounts.Where(a => accountIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id);
            foreach (var id in accountIds)
            {
                if (!accounts.TryGetValue(id, out var acc))
                    throw AccountingException.Unprocessable($"Account {id} does not exist.");
                if (acc.IsGroup)
                    throw AccountingException.Unprocessable($"Account '{acc.Name}' is a group; postings are only allowed on ledgers.");
                if (!acc.IsActive)
                    throw AccountingException.Unprocessable($"Account '{acc.Name}' is inactive.");
            }

            var periodId = await _periods.ResolvePostablePeriodIdAsync(request.Date);
            var voucherNo = await GenerateVoucherNoAsync(request.VoucherType, request.Date);

            var voucher = new Voucher
            {
                VoucherType = request.VoucherType,
                VoucherNo = voucherNo,
                Date = request.Date,
                PeriodId = periodId,
                Narration = request.Narration?.Trim(),
                Status = VoucherStatus.Posted,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                Lines = lines.Select(l => new VoucherLine
                {
                    AccountId = l.AccountId,
                    DrCr = l.DrCr,
                    Amount = Math.Round(l.Amount, 2),
                    LineNarration = l.LineNarration?.Trim()
                }).ToList()
            };

            _context.Vouchers.Add(voucher);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Posted {Type} voucher {No} for {Amount:0.00}.", voucher.VoucherType, voucher.VoucherNo, totalDr);
            return (await GetByIdAsync(voucher.Id))!;
        }

        public async Task<VoucherDto?> ReverseAsync(long id, ReverseVoucherRequest request, int? userId)
        {
            var original = await _context.Vouchers.Include(v => v.Lines).FirstOrDefaultAsync(v => v.Id == id);
            if (original == null) return null;
            if (original.Status == VoucherStatus.Reversed)
                throw AccountingException.Unprocessable("This voucher has already been reversed.");
            if (original.Status != VoucherStatus.Posted)
                throw AccountingException.Unprocessable("Only a posted voucher can be reversed.");

            var date = request.Date ?? original.Date;
            var periodId = await _periods.ResolvePostablePeriodIdAsync(date);
            var voucherNo = await GenerateVoucherNoAsync(original.VoucherType, date);

            var reversal = new Voucher
            {
                VoucherType = original.VoucherType,
                VoucherNo = voucherNo,
                Date = date,
                PeriodId = periodId,
                Narration = request.Narration?.Trim() ?? $"Reversal of {original.VoucherNo}",
                Status = VoucherStatus.Posted,
                ReversalOfId = original.Id,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                Lines = original.Lines.Select(l => new VoucherLine
                {
                    AccountId = l.AccountId,
                    DrCr = l.DrCr == DrCr.DR ? DrCr.CR : DrCr.DR,   // mirror the sides
                    Amount = l.Amount,
                    LineNarration = l.LineNarration
                }).ToList()
            };

            original.Status = VoucherStatus.Reversed;
            original.UpdatedAt = DateTime.UtcNow;

            _context.Vouchers.Add(reversal);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Reversed voucher {Orig} via {New}.", original.VoucherNo, reversal.VoucherNo);
            return (await GetByIdAsync(reversal.Id))!;
        }

        private async Task<string> GenerateVoucherNoAsync(VoucherType type, DateOnly date)
        {
            var prefix = Prefixes[type];
            var startYear = date.Month >= 4 ? date.Year : date.Year - 1;
            var fy = $"{startYear}-{(startYear + 1) % 100:D2}";

            // Next sequence = existing count of this type in this FY window + 1; retry on the unique-index race.
            var fyStart = new DateOnly(startYear, 4, 1);
            var fyEnd = new DateOnly(startYear + 1, 3, 31);
            var count = await _context.Vouchers.CountAsync(v => v.VoucherType == type && v.Date >= fyStart && v.Date <= fyEnd);

            for (var n = count + 1; ; n++)
            {
                var candidate = $"{prefix}-{fy}-{n:D4}";
                if (!await _context.Vouchers.AnyAsync(v => v.VoucherType == type && v.VoucherNo == candidate))
                    return candidate;
            }
        }

        private static VoucherDto MapDetail(Voucher v)
        {
            var lines = v.Lines.Select(l => new VoucherLineDto
            {
                Id = l.Id,
                AccountId = l.AccountId,
                AccountCode = l.Account?.Code ?? string.Empty,
                AccountName = l.Account?.Name ?? string.Empty,
                DrCr = l.DrCr.ToString(),
                Amount = l.Amount,
                LineNarration = l.LineNarration
            }).ToList();

            return new VoucherDto
            {
                Id = v.Id,
                VoucherType = v.VoucherType.ToString(),
                VoucherNo = v.VoucherNo,
                Date = v.Date,
                PeriodId = v.PeriodId,
                Narration = v.Narration,
                Status = v.Status.ToString(),
                SourceType = v.SourceType,
                SourceId = v.SourceId,
                ReversalOfId = v.ReversalOfId,
                CreatedBy = v.CreatedBy,
                CreatedAt = v.CreatedAt,
                TotalDebit = v.Lines.Where(l => l.DrCr == DrCr.DR).Sum(l => l.Amount),
                TotalCredit = v.Lines.Where(l => l.DrCr == DrCr.CR).Sum(l => l.Amount),
                Lines = lines
            };
        }
    }
}
