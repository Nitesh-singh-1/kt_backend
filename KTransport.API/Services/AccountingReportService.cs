using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class AccountingReportService : IAccountingReportService
    {
        private readonly KTransportDbContext _context;

        public AccountingReportService(KTransportDbContext context)
        {
            _context = context;
        }

        // A Draft voucher is not part of the books; Posted and Reversed both contribute
        // (a reversed voucher keeps its lines, and its reversal adds mirror lines that net to zero).
        private IQueryable<VoucherLine> BookLines =>
            _context.VoucherLines.AsNoTracking().Where(l => l.Voucher!.Status != VoucherStatus.Draft);

        private static decimal Signed(DrCr side, decimal amount) => side == DrCr.DR ? amount : -amount;

        public async Task<List<DayBookEntryDto>> GetDayBookAsync(DateOnly from, DateOnly to)
        {
            var rows = await BookLines
                .Where(l => l.Voucher!.Date >= from && l.Voucher.Date <= to)
                .OrderBy(l => l.Voucher!.Date).ThenBy(l => l.VoucherId).ThenBy(l => l.Id)
                .Select(l => new DayBookEntryDto
                {
                    VoucherId = l.VoucherId,
                    VoucherType = l.Voucher!.VoucherType.ToString(),
                    VoucherNo = l.Voucher.VoucherNo,
                    Date = l.Voucher.Date,
                    AccountCode = l.Account!.Code,
                    AccountName = l.Account.Name,
                    DrCr = l.DrCr.ToString(),
                    Amount = l.Amount,
                    Narration = l.LineNarration ?? l.Voucher.Narration
                })
                .ToListAsync();
            return rows;
        }

        public async Task<AccountLedgerDto?> GetLedgerAsync(long accountId, DateOnly from, DateOnly to)
        {
            var account = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId);
            if (account == null) return null;

            // Opening = recorded opening balance(s) + net of postings strictly before `from`.
            var openingBase = await _context.OpeningBalances.AsNoTracking()
                .Where(o => o.AccountId == accountId)
                .Select(o => new { o.DrCr, o.Amount }).ToListAsync();
            var openingSigned = openingBase.Sum(o => Signed(o.DrCr, o.Amount));

            var priorSigned = await BookLines
                .Where(l => l.AccountId == accountId && l.Voucher!.Date < from)
                .Select(l => new { l.DrCr, l.Amount }).ToListAsync();
            openingSigned += priorSigned.Sum(l => Signed(l.DrCr, l.Amount));

            var inRange = await BookLines
                .Where(l => l.AccountId == accountId && l.Voucher!.Date >= from && l.Voucher.Date <= to)
                .OrderBy(l => l.Voucher!.Date).ThenBy(l => l.VoucherId).ThenBy(l => l.Id)
                .Select(l => new
                {
                    l.Voucher!.Date,
                    Type = l.Voucher.VoucherType,
                    l.Voucher.VoucherNo,
                    Narration = l.LineNarration ?? l.Voucher.Narration,
                    l.DrCr,
                    l.Amount
                }).ToListAsync();

            var running = openingSigned;
            var lines = new List<LedgerLineDto>();
            foreach (var l in inRange)
            {
                running += Signed(l.DrCr, l.Amount);
                lines.Add(new LedgerLineDto
                {
                    Date = l.Date,
                    VoucherType = l.Type.ToString(),
                    VoucherNo = l.VoucherNo,
                    Narration = l.Narration,
                    Dr = l.DrCr == DrCr.DR ? l.Amount : 0m,
                    Cr = l.DrCr == DrCr.CR ? l.Amount : 0m,
                    RunningBalance = running
                });
            }

            return new AccountLedgerDto
            {
                AccountId = account.Id,
                AccountCode = account.Code,
                AccountName = account.Name,
                OpeningDr = openingSigned > 0 ? openingSigned : 0m,
                OpeningCr = openingSigned < 0 ? -openingSigned : 0m,
                Lines = lines,
                ClosingDr = running > 0 ? running : 0m,
                ClosingCr = running < 0 ? -running : 0m
            };
        }

        public async Task<TrialBalanceDto> GetTrialBalanceAsync(DateOnly asOf)
        {
            // Net signed balance per account = opening + postings up to asOf.
            var opening = await _context.OpeningBalances.AsNoTracking()
                .Select(o => new { o.AccountId, o.DrCr, o.Amount }).ToListAsync();
            var postings = await BookLines
                .Where(l => l.Voucher!.Date <= asOf)
                .Select(l => new { l.AccountId, l.DrCr, l.Amount }).ToListAsync();

            var net = new Dictionary<long, decimal>();
            foreach (var o in opening)
                net[o.AccountId] = net.GetValueOrDefault(o.AccountId) + Signed(o.DrCr, o.Amount);
            foreach (var p in postings)
                net[p.AccountId] = net.GetValueOrDefault(p.AccountId) + Signed(p.DrCr, p.Amount);

            var accountIds = net.Keys.ToList();
            var accounts = await _context.Accounts.AsNoTracking()
                .Where(a => accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id);

            var rows = new List<TrialBalanceRowDto>();
            foreach (var kv in net)
            {
                var bal = Math.Round(kv.Value, 2);
                if (bal == 0m) continue;
                if (!accounts.TryGetValue(kv.Key, out var acc)) continue;
                rows.Add(new TrialBalanceRowDto
                {
                    AccountId = acc.Id,
                    Code = acc.Code,
                    Name = acc.Name,
                    AccountType = acc.AccountType.ToString(),
                    Debit = bal > 0 ? bal : 0m,
                    Credit = bal < 0 ? -bal : 0m
                });
            }

            rows = rows.OrderBy(r => r.Code).ToList();
            var totalDr = Math.Round(rows.Sum(r => r.Debit), 2);
            var totalCr = Math.Round(rows.Sum(r => r.Credit), 2);

            return new TrialBalanceDto
            {
                AsOf = asOf,
                Rows = rows,
                TotalDebit = totalDr,
                TotalCredit = totalCr,
                Balanced = totalDr == totalCr
            };
        }
    }
}
