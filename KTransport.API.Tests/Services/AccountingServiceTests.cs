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
}
