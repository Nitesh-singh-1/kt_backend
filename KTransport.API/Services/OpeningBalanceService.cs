using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    /// <summary>
    /// TASK-016 Phase 1 (review B3). Lets a tenant record per-financial-year opening balances so
    /// the ledger can start from existing figures without replaying history. A set is a full
    /// replace of that year's rows and must balance (ΣDR = ΣCR), the same invariant a voucher
    /// obeys — otherwise the trial balance could never balance.
    /// </summary>
    public class OpeningBalanceService : IOpeningBalanceService
    {
        private readonly KTransportDbContext _context;

        public OpeningBalanceService(KTransportDbContext context)
        {
            _context = context;
        }

        public async Task<OpeningBalanceSetDto?> GetAsync(long periodId)
        {
            var period = await _context.FinancialPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == periodId);
            if (period == null) return null;

            var rows = await _context.OpeningBalances.AsNoTracking()
                .Where(o => o.PeriodId == periodId)
                .Select(o => new OpeningBalanceRowDto
                {
                    AccountId = o.AccountId,
                    AccountCode = o.Account!.Code,
                    AccountName = o.Account.Name,
                    AccountType = o.Account.AccountType.ToString(),
                    DrCr = o.DrCr.ToString(),
                    Amount = o.Amount
                })
                .ToListAsync();

            rows = rows.OrderBy(r => r.AccountCode).ToList();
            return Build(period, rows);
        }

        public async Task<OpeningBalanceSetDto?> SetAsync(SetOpeningBalancesRequest request)
        {
            var period = await _context.FinancialPeriods.FirstOrDefaultAsync(p => p.Id == request.PeriodId);
            if (period == null) return null;
            if (period.Status == PeriodStatus.Closed)
                throw AccountingException.Conflict($"Financial year '{period.FyLabel}' is closed; opening balances cannot be changed.");

            // Drop zero rows (a zero opening is simply the absence of a row).
            var input = (request.Lines ?? new List<OpeningBalanceLineInput>())
                .Where(l => Math.Round(l.Amount, 2) != 0m)
                .ToList();

            if (input.Any(l => l.Amount < 0))
                throw AccountingException.Unprocessable("Opening amount cannot be negative. Use the Dr/Cr side to express direction.");

            var dupes = input.GroupBy(l => l.AccountId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Count > 0)
                throw AccountingException.Unprocessable("An account appears more than once. Record a single net opening figure per account.");

            // Accounts must exist, be active, and be postable ledgers (not groups) — mirrors VoucherService.
            var accountIds = input.Select(l => l.AccountId).Distinct().ToList();
            var accounts = await _context.Accounts.Where(a => accountIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id);
            foreach (var id in accountIds)
            {
                if (!accounts.TryGetValue(id, out var acc))
                    throw AccountingException.Unprocessable($"Account {id} does not exist.");
                if (acc.IsGroup)
                    throw AccountingException.Unprocessable($"Account '{acc.Name}' is a group; opening balances are only allowed on ledgers.");
                if (!acc.IsActive)
                    throw AccountingException.Unprocessable($"Account '{acc.Name}' is inactive.");
            }

            var totalDr = Math.Round(input.Where(l => l.DrCr == DrCr.DR).Sum(l => l.Amount), 2);
            var totalCr = Math.Round(input.Where(l => l.DrCr == DrCr.CR).Sum(l => l.Amount), 2);
            if (totalDr != totalCr)
                throw AccountingException.Unprocessable($"Opening balances are not balanced: debits {totalDr:0.00} ≠ credits {totalCr:0.00}.");

            // Full replace of this period's rows.
            var existing = await _context.OpeningBalances.Where(o => o.PeriodId == request.PeriodId).ToListAsync();
            if (existing.Count > 0) _context.OpeningBalances.RemoveRange(existing);

            foreach (var l in input)
            {
                _context.OpeningBalances.Add(new OpeningBalance
                {
                    AccountId = l.AccountId,
                    PeriodId = request.PeriodId,
                    DrCr = l.DrCr,
                    Amount = Math.Round(l.Amount, 2),
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return await GetAsync(request.PeriodId);
        }

        private static OpeningBalanceSetDto Build(FinancialPeriod period, List<OpeningBalanceRowDto> rows)
        {
            var totalDr = Math.Round(rows.Where(r => r.DrCr == "DR").Sum(r => r.Amount), 2);
            var totalCr = Math.Round(rows.Where(r => r.DrCr == "CR").Sum(r => r.Amount), 2);
            return new OpeningBalanceSetDto
            {
                PeriodId = period.Id,
                FyLabel = period.FyLabel,
                PeriodStatus = period.Status.ToString(),
                Rows = rows,
                TotalDebit = totalDr,
                TotalCredit = totalCr,
                Balanced = totalDr == totalCr
            };
        }
    }
}
