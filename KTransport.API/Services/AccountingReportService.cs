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

        /// <summary>
        /// Per-account opening "cutoff": the opening row(s) of the latest financial period whose
        /// StartDate is on/before <paramref name="boundary"/>. Returns that period's StartDate (so
        /// callers only count postings from the cutoff forward — postings before it are already
        /// baked into the opening figure) and the signed opening amount. Accounts with no qualifying
        /// opening row are absent (treat as cutoff MinValue / opening 0 = count all postings).
        /// This is the fix for review H2: summing every FY's opening rows double-counted
        /// carried-forward balances.
        /// </summary>
        private async Task<Dictionary<long, (DateOnly CutoffStart, decimal OpeningSigned)>> ComputeOpeningCutoffsAsync(
            DateOnly boundary, long? accountId)
        {
            var q = _context.OpeningBalances.AsNoTracking().Where(o => o.Period!.StartDate <= boundary);
            if (accountId.HasValue) q = q.Where(o => o.AccountId == accountId.Value);

            var rows = await q
                .Select(o => new { o.AccountId, Start = o.Period!.StartDate, o.DrCr, o.Amount })
                .ToListAsync();

            var result = new Dictionary<long, (DateOnly, decimal)>();
            foreach (var g in rows.GroupBy(r => r.AccountId))
            {
                var cutoffStart = g.Max(r => r.Start);
                var openingSigned = g.Where(r => r.Start == cutoffStart).Sum(r => Signed(r.DrCr, r.Amount));
                result[g.Key] = (cutoffStart, openingSigned);
            }
            return result;
        }

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

            // Opening as of `from` = the opening row from the latest FY whose start is on/before
            // `from` (NOT every FY's rows — that double-counted; review H2), plus the net of
            // postings from that cutoff's start up to (but excluding) `from`.
            var cutoffs = await ComputeOpeningCutoffsAsync(from, accountId);
            var (cutoffStart, openingSigned) = cutoffs.TryGetValue(accountId, out var cut)
                ? cut : (DateOnly.MinValue, 0m);

            var priorSigned = await BookLines
                .Where(l => l.AccountId == accountId && l.Voucher!.Date >= cutoffStart && l.Voucher.Date < from)
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
            // Net signed balance per account = the single applicable opening (latest FY whose start
            // is on/before asOf) + postings on/after that cutoff up to asOf. Summing every FY's
            // opening rows double-counted carried-forward figures (review H2).
            var cutoffs = await ComputeOpeningCutoffsAsync(asOf, null);
            var postings = await BookLines
                .Where(l => l.Voucher!.Date <= asOf)
                .Select(l => new { l.AccountId, Date = l.Voucher!.Date, l.DrCr, l.Amount }).ToListAsync();

            var net = new Dictionary<long, decimal>();
            foreach (var kv in cutoffs)
                net[kv.Key] = net.GetValueOrDefault(kv.Key) + kv.Value.OpeningSigned;
            foreach (var p in postings)
            {
                var cutoffStart = cutoffs.TryGetValue(p.AccountId, out var c) ? c.CutoffStart : DateOnly.MinValue;
                if (p.Date >= cutoffStart)
                    net[p.AccountId] = net.GetValueOrDefault(p.AccountId) + Signed(p.DrCr, p.Amount);
            }

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
