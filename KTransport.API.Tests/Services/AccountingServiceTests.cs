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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KTransport.API.Tests.Services;

/// <summary>
/// TASK-016 Phase 1 — accounting ledger engine. Covers the balancing invariant (service half),
/// seeding idempotency, posted-immutability via reversal, closed-period locking, and the
/// trial-balance equality guarantee. The DB trigger/check are Postgres-only and not exercised by
/// the InMemory provider, so these assert the service-level enforcement.
/// </summary>
public class AccountingServiceTests
{
    private sealed class StubTenantContext : ITenantContext
    {
        public Guid CurrentTenantId { get; private set; } = TenantConstants.DefaultTenantId;
        public bool HasTenant => true;
        public void SetTenantId(Guid tenantId) => CurrentTenantId = tenantId;
    }

    private static KTransportDbContext NewContext()
    {
        var opts = new DbContextOptionsBuilder<KTransportDbContext>()
            .UseInMemoryDatabase(databaseName: $"AcctTests_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new KTransportDbContext(opts, new StubTenantContext());
    }

    private static AccountService NewAccounts(KTransportDbContext ctx)
        => new(ctx, NullLogger<AccountService>.Instance);

    private static VoucherService NewVouchers(KTransportDbContext ctx, out FinancialPeriodService periods)
    {
        periods = new FinancialPeriodService(ctx);
        return new VoucherService(ctx, periods, NullLogger<VoucherService>.Instance);
    }

    /// <summary>Creates a postable ledger under the given system group code; returns its id.</summary>
    private static async Task<long> LedgerUnderAsync(AccountService accounts, KTransportDbContext ctx, string groupCode, string name)
    {
        var group = await ctx.Accounts.FirstAsync(a => a.Code == groupCode);
        var dto = await accounts.CreateAsync(new CreateAccountRequest { ParentId = group.Id, Name = name });
        return dto.Id;
    }

    [Fact]
    public async Task Seeding_Is_Idempotent_And_Creates_28_System_Groups()
    {
        using var ctx = NewContext();
        var accounts = NewAccounts(ctx);

        await accounts.EnsureSeededAsync();
        await accounts.EnsureSeededAsync(); // second call must not duplicate

        var all = await ctx.Accounts.ToListAsync();
        Assert.Equal(28, all.Count);
        Assert.All(all, a => Assert.True(a.IsSystem && a.IsGroup));
        Assert.Equal(13, all.Count(a => a.ParentId != null)); // 13 sub-groups
    }

    [Fact]
    public async Task CreateVoucher_Balanced_Posts_With_Number_And_Lines()
    {
        using var ctx = NewContext();
        var accounts = NewAccounts(ctx);
        await accounts.EnsureSeededAsync();
        var cash = await LedgerUnderAsync(accounts, ctx, "1120", "Cash");
        var sales = await LedgerUnderAsync(accounts, ctx, "4000", "Freight Income");
        var vouchers = NewVouchers(ctx, out _);

        var v = await vouchers.CreateAsync(new CreateVoucherRequest
        {
            VoucherType = VoucherType.Receipt,
            Date = new DateOnly(2026, 5, 10),
            Narration = "Cash freight",
            Lines = new List<VoucherLineInput>
            {
                new() { AccountId = cash,  DrCr = DrCr.DR, Amount = 1000m },
                new() { AccountId = sales, DrCr = DrCr.CR, Amount = 1000m },
            }
        }, userId: 1);

        Assert.Equal("Posted", v.Status);
        Assert.StartsWith("RCT-2026-27-", v.VoucherNo);
        Assert.Equal(1000m, v.TotalDebit);
        Assert.Equal(1000m, v.TotalCredit);
        Assert.Equal(2, v.Lines.Count);
    }

    [Fact]
    public async Task CreateVoucher_Unbalanced_Throws_422()
    {
        using var ctx = NewContext();
        var accounts = NewAccounts(ctx);
        await accounts.EnsureSeededAsync();
        var cash = await LedgerUnderAsync(accounts, ctx, "1120", "Cash");
        var sales = await LedgerUnderAsync(accounts, ctx, "4000", "Freight Income");
        var vouchers = NewVouchers(ctx, out _);

        var ex = await Assert.ThrowsAsync<AccountingException>(() => vouchers.CreateAsync(new CreateVoucherRequest
        {
            VoucherType = VoucherType.Journal,
            Date = new DateOnly(2026, 5, 10),
            Lines = new List<VoucherLineInput>
            {
                new() { AccountId = cash,  DrCr = DrCr.DR, Amount = 1000m },
                new() { AccountId = sales, DrCr = DrCr.CR, Amount = 900m },
            }
        }, userId: 1));

        Assert.Equal(422, ex.StatusCode);
        Assert.Empty(await ctx.Vouchers.ToListAsync()); // nothing persisted
    }

    [Fact]
    public async Task CreateVoucher_Rejects_Posting_On_A_Group_Account()
    {
        using var ctx = NewContext();
        var accounts = NewAccounts(ctx);
        await accounts.EnsureSeededAsync();
        var cash = await LedgerUnderAsync(accounts, ctx, "1120", "Cash");
        var salesGroup = await ctx.Accounts.FirstAsync(a => a.Code == "4000"); // a group, not a ledger
        var vouchers = NewVouchers(ctx, out _);

        await Assert.ThrowsAsync<AccountingException>(() => vouchers.CreateAsync(new CreateVoucherRequest
        {
            VoucherType = VoucherType.Journal,
            Date = new DateOnly(2026, 5, 10),
            Lines = new List<VoucherLineInput>
            {
                new() { AccountId = cash,           DrCr = DrCr.DR, Amount = 100m },
                new() { AccountId = salesGroup.Id,  DrCr = DrCr.CR, Amount = 100m },
            }
        }, userId: 1));
    }

    [Fact]
    public async Task Reverse_Creates_Mirror_And_Flips_Original_To_Reversed()
    {
        using var ctx = NewContext();
        var accounts = NewAccounts(ctx);
        await accounts.EnsureSeededAsync();
        var cash = await LedgerUnderAsync(accounts, ctx, "1120", "Cash");
        var sales = await LedgerUnderAsync(accounts, ctx, "4000", "Freight Income");
        var vouchers = NewVouchers(ctx, out _);

        var v = await vouchers.CreateAsync(new CreateVoucherRequest
        {
            VoucherType = VoucherType.Receipt,
            Date = new DateOnly(2026, 5, 10),
            Lines = new List<VoucherLineInput>
            {
                new() { AccountId = cash,  DrCr = DrCr.DR, Amount = 1000m },
                new() { AccountId = sales, DrCr = DrCr.CR, Amount = 1000m },
            }
        }, userId: 1);

        var rev = await vouchers.ReverseAsync(v.Id, new ReverseVoucherRequest(), userId: 1);

        Assert.NotNull(rev);
        Assert.Equal(v.Id, rev!.ReversalOfId);
        var original = await ctx.Vouchers.AsNoTracking().FirstAsync(x => x.Id == v.Id);
        Assert.Equal(VoucherStatus.Reversed, original.Status);

        // The cash line was DR in the original; in the reversal it must be CR.
        var revCashLine = rev.Lines.First(l => l.AccountId == cash);
        Assert.Equal("CR", revCashLine.DrCr);

        // Reversing again is rejected.
        await Assert.ThrowsAsync<AccountingException>(() => vouchers.ReverseAsync(v.Id, new ReverseVoucherRequest(), userId: 1));
    }

    [Fact]
    public async Task Posting_In_A_Closed_Period_Is_Rejected()
    {
        using var ctx = NewContext();
        var accounts = NewAccounts(ctx);
        await accounts.EnsureSeededAsync();
        var cash = await LedgerUnderAsync(accounts, ctx, "1120", "Cash");
        var sales = await LedgerUnderAsync(accounts, ctx, "4000", "Freight Income");
        var vouchers = NewVouchers(ctx, out var periods);

        var period = await periods.CreateAsync(new CreatePeriodRequest { FyLabel = "2026-27" });
        await periods.CloseAsync(period.Id);

        var ex = await Assert.ThrowsAsync<AccountingException>(() => vouchers.CreateAsync(new CreateVoucherRequest
        {
            VoucherType = VoucherType.Journal,
            Date = new DateOnly(2026, 6, 1), // inside the closed FY
            Lines = new List<VoucherLineInput>
            {
                new() { AccountId = cash,  DrCr = DrCr.DR, Amount = 500m },
                new() { AccountId = sales, DrCr = DrCr.CR, Amount = 500m },
            }
        }, userId: 1));

        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public async Task TrialBalance_Debits_Equal_Credits_After_Postings()
    {
        using var ctx = NewContext();
        var accounts = NewAccounts(ctx);
        await accounts.EnsureSeededAsync();
        var cash = await LedgerUnderAsync(accounts, ctx, "1120", "Cash");
        var sales = await LedgerUnderAsync(accounts, ctx, "4000", "Freight Income");
        var debtor = await LedgerUnderAsync(accounts, ctx, "1160", "ACME Ltd");
        var vouchers = NewVouchers(ctx, out _);

        await vouchers.CreateAsync(new CreateVoucherRequest
        {
            VoucherType = VoucherType.Sales, Date = new DateOnly(2026, 5, 1),
            Lines = new List<VoucherLineInput>
            {
                new() { AccountId = debtor, DrCr = DrCr.DR, Amount = 1180m },
                new() { AccountId = sales,  DrCr = DrCr.CR, Amount = 1180m },
            }
        }, 1);
        await vouchers.CreateAsync(new CreateVoucherRequest
        {
            VoucherType = VoucherType.Receipt, Date = new DateOnly(2026, 5, 20),
            Lines = new List<VoucherLineInput>
            {
                new() { AccountId = cash,   DrCr = DrCr.DR, Amount = 1180m },
                new() { AccountId = debtor, DrCr = DrCr.CR, Amount = 1180m },
            }
        }, 1);

        var reports = new AccountingReportService(ctx);
        var tb = await reports.GetTrialBalanceAsync(new DateOnly(2026, 5, 31));

        Assert.True(tb.Balanced);
        Assert.Equal(tb.TotalDebit, tb.TotalCredit);
        // Cash 1180 Dr, Sales 1180 Cr, Debtor nets to zero (dropped from TB).
        Assert.Equal(1180m, tb.TotalDebit);
        Assert.DoesNotContain(tb.Rows, r => r.AccountId == debtor);
    }

    // ---------------- Opening balances (review B3) ----------------

    private static OpeningBalanceService NewOpening(KTransportDbContext ctx) => new(ctx);

    [Fact]
    public async Task OpeningBalances_Set_Balanced_Persists_And_Reads_Back()
    {
        using var ctx = NewContext();
        var accounts = NewAccounts(ctx);
        await accounts.EnsureSeededAsync();
        var cash = await LedgerUnderAsync(accounts, ctx, "1120", "Cash");
        var cap = await LedgerUnderAsync(accounts, ctx, "4000", "Opening Equity");
        var periods = new FinancialPeriodService(ctx);
        var p = await periods.CreateAsync(new CreatePeriodRequest { FyLabel = "2026-27" });
        var opening = NewOpening(ctx);

        var set = await opening.SetAsync(new SetOpeningBalancesRequest
        {
            PeriodId = p.Id,
            Lines = new List<OpeningBalanceLineInput>
            {
                new() { AccountId = cash, DrCr = DrCr.DR, Amount = 1000m },
                new() { AccountId = cap,  DrCr = DrCr.CR, Amount = 1000m },
            }
        });

        Assert.NotNull(set);
        Assert.True(set!.Balanced);
        Assert.Equal(1000m, set.TotalDebit);
        Assert.Equal(2, set.Rows.Count);

        var read = await opening.GetAsync(p.Id);
        Assert.Equal(1000m, read!.TotalCredit);
    }

    [Fact]
    public async Task OpeningBalances_Set_Is_Full_Replace()
    {
        using var ctx = NewContext();
        var accounts = NewAccounts(ctx);
        await accounts.EnsureSeededAsync();
        var cash = await LedgerUnderAsync(accounts, ctx, "1120", "Cash");
        var cap = await LedgerUnderAsync(accounts, ctx, "4000", "Opening Equity");
        var periods = new FinancialPeriodService(ctx);
        var p = await periods.CreateAsync(new CreatePeriodRequest { FyLabel = "2026-27" });
        var opening = NewOpening(ctx);

        await opening.SetAsync(new SetOpeningBalancesRequest
        {
            PeriodId = p.Id,
            Lines = new List<OpeningBalanceLineInput>
            {
                new() { AccountId = cash, DrCr = DrCr.DR, Amount = 1000m },
                new() { AccountId = cap,  DrCr = DrCr.CR, Amount = 1000m },
            }
        });
        // Replace with a smaller balanced set.
        var set = await opening.SetAsync(new SetOpeningBalancesRequest
        {
            PeriodId = p.Id,
            Lines = new List<OpeningBalanceLineInput>
            {
                new() { AccountId = cash, DrCr = DrCr.DR, Amount = 250m },
                new() { AccountId = cap,  DrCr = DrCr.CR, Amount = 250m },
            }
        });

        Assert.Equal(250m, set!.TotalDebit);
        Assert.Equal(2, await ctx.OpeningBalances.CountAsync(o => o.PeriodId == p.Id));
    }

    [Fact]
    public async Task OpeningBalances_Unbalanced_Throws_422()
    {
        using var ctx = NewContext();
        var accounts = NewAccounts(ctx);
        await accounts.EnsureSeededAsync();
        var cash = await LedgerUnderAsync(accounts, ctx, "1120", "Cash");
        var cap = await LedgerUnderAsync(accounts, ctx, "4000", "Opening Equity");
        var periods = new FinancialPeriodService(ctx);
        var p = await periods.CreateAsync(new CreatePeriodRequest { FyLabel = "2026-27" });
        var opening = NewOpening(ctx);

        var ex = await Assert.ThrowsAsync<AccountingException>(() => opening.SetAsync(new SetOpeningBalancesRequest
        {
            PeriodId = p.Id,
            Lines = new List<OpeningBalanceLineInput>
            {
                new() { AccountId = cash, DrCr = DrCr.DR, Amount = 1000m },
                new() { AccountId = cap,  DrCr = DrCr.CR, Amount = 400m },
            }
        }));
        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public async Task OpeningBalances_Rejects_Group_Account()
    {
        using var ctx = NewContext();
        var accounts = NewAccounts(ctx);
        await accounts.EnsureSeededAsync();
        var group = await ctx.Accounts.FirstAsync(a => a.Code == "1120"); // a seeded system group
        var cash = await LedgerUnderAsync(accounts, ctx, "1120", "Cash");
        var periods = new FinancialPeriodService(ctx);
        var p = await periods.CreateAsync(new CreatePeriodRequest { FyLabel = "2026-27" });
        var opening = NewOpening(ctx);

        var ex = await Assert.ThrowsAsync<AccountingException>(() => opening.SetAsync(new SetOpeningBalancesRequest
        {
            PeriodId = p.Id,
            Lines = new List<OpeningBalanceLineInput>
            {
                new() { AccountId = group.Id, DrCr = DrCr.DR, Amount = 1000m },
                new() { AccountId = cash,     DrCr = DrCr.CR, Amount = 1000m },
            }
        }));
        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public async Task OpeningBalances_In_Closed_Year_Rejected_409()
    {
        using var ctx = NewContext();
        var accounts = NewAccounts(ctx);
        await accounts.EnsureSeededAsync();
        var cash = await LedgerUnderAsync(accounts, ctx, "1120", "Cash");
        var cap = await LedgerUnderAsync(accounts, ctx, "4000", "Opening Equity");
        var periods = new FinancialPeriodService(ctx);
        var p = await periods.CreateAsync(new CreatePeriodRequest { FyLabel = "2026-27" });
        await periods.CloseAsync(p.Id);
        var opening = NewOpening(ctx);

        var ex = await Assert.ThrowsAsync<AccountingException>(() => opening.SetAsync(new SetOpeningBalancesRequest
        {
            PeriodId = p.Id,
            Lines = new List<OpeningBalanceLineInput>
            {
                new() { AccountId = cash, DrCr = DrCr.DR, Amount = 1000m },
                new() { AccountId = cap,  DrCr = DrCr.CR, Amount = 1000m },
            }
        }));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task TrialBalance_And_Ledger_Do_Not_Double_Count_Opening_Across_Years()
    {
        using var ctx = NewContext();
        var accounts = NewAccounts(ctx);
        await accounts.EnsureSeededAsync();
        var cash = await LedgerUnderAsync(accounts, ctx, "1120", "Cash");
        var cap = await LedgerUnderAsync(accounts, ctx, "4000", "Opening Equity");
        var periods = new FinancialPeriodService(ctx);
        var p1 = await periods.CreateAsync(new CreatePeriodRequest { FyLabel = "2025-26" });
        var p2 = await periods.CreateAsync(new CreatePeriodRequest { FyLabel = "2026-27" });
        var opening = NewOpening(ctx);

        // The SAME 1000 Dr carried into BOTH years (a realistic carry-forward). The old code
        // summed both rows and reported 2000; the fix must use only the latest applicable year.
        foreach (var pid in new[] { p1.Id, p2.Id })
            await opening.SetAsync(new SetOpeningBalancesRequest
            {
                PeriodId = pid,
                Lines = new List<OpeningBalanceLineInput>
                {
                    new() { AccountId = cash, DrCr = DrCr.DR, Amount = 1000m },
                    new() { AccountId = cap,  DrCr = DrCr.CR, Amount = 1000m },
                }
            });

        var reports = new AccountingReportService(ctx);
        var tb = await reports.GetTrialBalanceAsync(new DateOnly(2026, 5, 31));

        var cashRow = tb.Rows.Single(r => r.AccountId == cash);
        Assert.Equal(1000m, cashRow.Debit);   // NOT 2000
        Assert.True(tb.Balanced);

        var led = await reports.GetLedgerAsync(cash, new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));
        Assert.Equal(1000m, led!.OpeningDr);   // NOT 2000
    }
}
