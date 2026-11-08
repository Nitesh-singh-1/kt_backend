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
/// Phase 2 auto-posting engine. Exercises the five source builders (each must balance ΣDR=ΣCR and
/// carry source_type+source_id), idempotent double-dispatch, reverse+repost, closed-period -> Deferred,
/// and the default-OFF skip. The Postgres partial unique index + balancing trigger are not run by the
/// InMemory provider, so idempotency relies on the service pre-check and balancing on VoucherService.
/// </summary>
public class AutoPostingServiceTests
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
            .UseInMemoryDatabase(databaseName: $"AutoPostTests_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new KTransportDbContext(opts, new StubTenantContext());
    }

    private static AutoPostingService NewEngine(KTransportDbContext ctx)
    {
        var periods = new FinancialPeriodService(ctx);
        var vouchers = new VoucherService(ctx, periods, NullLogger<VoucherService>.Instance);
        var accounts = new AccountService(ctx, NullLogger<AccountService>.Instance);
        return new AutoPostingService(ctx, vouchers, accounts, new FlatGstPostingResolver(), NullLogger<AutoPostingService>.Instance);
    }

    private static async Task EnableAsync(KTransportDbContext ctx)
    {
        ctx.AccountingSettings.Add(new AccountingSettings { AutoPostingEnabled = true, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
    }

    private static async Task<Voucher> LiveVoucherAsync(KTransportDbContext ctx, string st, long id)
        => await ctx.Vouchers.Include(v => v.Lines)
            .FirstAsync(v => v.SourceType == st && v.SourceId == id && v.ReversalOfId == null && v.Status == VoucherStatus.Posted);

    private static void AssertBalanced(Voucher v)
    {
        var dr = v.Lines.Where(l => l.DrCr == DrCr.DR).Sum(l => l.Amount);
        var cr = v.Lines.Where(l => l.DrCr == DrCr.CR).Sum(l => l.Amount);
        Assert.True(dr > 0, "voucher has no debit");
        Assert.Equal(dr, cr);
    }

    // ---------------- per-source balance ----------------

    [Fact]
    public async Task Invoice_Posts_Balanced_Sales_Voucher_With_Source()
    {
        using var ctx = NewContext();
        var engine = NewEngine(ctx);
        var inv = new Invoice
        {
            InvoiceNo = "INV-1", InvoiceDate = new DateOnly(2026, 5, 10),
            PartyId = 5, PartyName = "ACME Ltd",
            SubTotal = 1000m, TaxRate = 5m, TaxAmount = 50m, OtherCharges = 20m, Discount = 10m,
            GrandTotal = 1060m, PaymentMode = "Cash", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.Invoices.Add(inv);
        await ctx.SaveChangesAsync();

        await engine.PostFromSourceAsync(AutoPostSourceTypes.Invoice, inv.Id);

        var v = await LiveVoucherAsync(ctx, AutoPostSourceTypes.Invoice, inv.Id);
        Assert.Equal(VoucherType.Sales, v.VoucherType);
        AssertBalanced(v);
        // A/R debtor sub-ledger created under Sundry Debtors (1160) for party 5.
        Assert.Contains(await ctx.Accounts.ToListAsync(), a => a.SourcePartyId == 5 && a.Code == "DR-5");
        // Freight income credit == SubTotal; Output GST credit == TaxAmount.
        var freight = await ctx.Accounts.FirstAsync(a => a.Code == WellKnownLedgers.FreightIncome);
        var gst = await ctx.Accounts.FirstAsync(a => a.Code == WellKnownLedgers.OutputGst);
        Assert.Equal(1000m, v.Lines.First(l => l.AccountId == freight.Id).Amount);
        Assert.Equal(50m, v.Lines.First(l => l.AccountId == gst.Id).Amount);
    }

    [Fact]
    public async Task Invoice_With_Zero_Tax_Posts_No_Gst_Leg()
    {
        using var ctx = NewContext();
        var engine = NewEngine(ctx);
        var inv = new Invoice
        {
            InvoiceNo = "INV-RCM", InvoiceDate = new DateOnly(2026, 5, 10),
            SubTotal = 1000m, TaxAmount = 0m, OtherCharges = 0m, Discount = 0m, GrandTotal = 1000m,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.Invoices.Add(inv);
        await ctx.SaveChangesAsync();

        await engine.PostFromSourceAsync(AutoPostSourceTypes.Invoice, inv.Id);

        var v = await LiveVoucherAsync(ctx, AutoPostSourceTypes.Invoice, inv.Id);
        AssertBalanced(v);
        var gst = await ctx.Accounts.FirstAsync(a => a.Code == WellKnownLedgers.OutputGst);
        Assert.DoesNotContain(v.Lines, l => l.AccountId == gst.Id); // RCM/exempt -> no GST leg
        Assert.Equal(2, v.Lines.Count); // Dr walk-in debtor / Cr freight income
    }

    [Fact]
    public async Task InvoicePayment_Posts_Balanced_Receipt()
    {
        using var ctx = NewContext();
        var engine = NewEngine(ctx);
        var inv = new Invoice { InvoiceNo = "INV-2", InvoiceDate = new DateOnly(2026, 5, 10), SubTotal = 600m, GrandTotal = 600m, PaidAmount = 600m, PaymentMode = "UPI", IsActive = true, CreatedAt = DateTime.UtcNow };
        ctx.Invoices.Add(inv);
        await ctx.SaveChangesAsync();

        await engine.PostFromSourceAsync(AutoPostSourceTypes.InvoicePayment, inv.Id);

        var v = await LiveVoucherAsync(ctx, AutoPostSourceTypes.InvoicePayment, inv.Id);
        Assert.Equal(VoucherType.Receipt, v.VoucherType);
        AssertBalanced(v);
        var bank = await ctx.Accounts.FirstAsync(a => a.Code == WellKnownLedgers.Bank);
        Assert.Equal(600m, v.Lines.First(l => l.AccountId == bank.Id && l.DrCr == DrCr.DR).Amount); // UPI -> BANK
    }

    [Fact]
    public async Task ShipmentReceipt_Posts_Balanced_Receipt()
    {
        using var ctx = NewContext();
        var engine = NewEngine(ctx);
        var s = new Shipment
        {
            ShipmentNo = "GR-1", ShipmentDate = new DateOnly(2026, 5, 10),
            SettledReceivedAmount = 500m, PaidAmount = 500m, SettledPaymentMode = "Cash",
            ConsignorPartyId = null, ConsignorName = "Cash Customer", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.Shipments.Add(s);
        await ctx.SaveChangesAsync();

        await engine.PostFromSourceAsync(AutoPostSourceTypes.Shipment, s.Id);

        var v = await LiveVoucherAsync(ctx, AutoPostSourceTypes.Shipment, s.Id);
        Assert.Equal(VoucherType.Receipt, v.VoucherType);
        AssertBalanced(v);
        var walkin = await ctx.Accounts.FirstAsync(a => a.Code == WellKnownLedgers.WalkinDebtor);
        Assert.Equal(500m, v.Lines.First(l => l.AccountId == walkin.Id && l.DrCr == DrCr.CR).Amount);
    }

    [Fact]
    public async Task TripExpense_Cash_Posts_Balanced_Payment()
    {
        using var ctx = NewContext();
        var engine = NewEngine(ctx);
        var e = new TripExpense { TripId = 1, ExpenseType = TripExpenseType.Fuel, Amount = 300m, PaymentMode = "Cash", ExpenseDate = new DateOnly(2026, 5, 10), CreatedAt = DateTime.UtcNow };
        ctx.TripExpenses.Add(e);
        await ctx.SaveChangesAsync();

        await engine.PostFromSourceAsync(AutoPostSourceTypes.TripExpense, e.Id);

        var v = await LiveVoucherAsync(ctx, AutoPostSourceTypes.TripExpense, e.Id);
        Assert.Equal(VoucherType.Payment, v.VoucherType);
        AssertBalanced(v);
        var fuel = await ctx.Accounts.FirstAsync(a => a.Code == WellKnownLedgers.FuelExpense);
        Assert.Equal(300m, v.Lines.First(l => l.AccountId == fuel.Id && l.DrCr == DrCr.DR).Amount);
    }

    [Fact]
    public async Task TripExpense_DriverMode_Credits_Driver_Advance()
    {
        using var ctx = NewContext();
        var engine = NewEngine(ctx);
        ctx.Trips.Add(new Trip { Id = 9, TripNo = "T-9", TripDate = new DateOnly(2026, 5, 10), DriverId = 7, DriverName = "Ravi", IsActive = true, CreatedAt = DateTime.UtcNow });
        var e = new TripExpense { TripId = 9, ExpenseType = TripExpenseType.Toll, Amount = 200m, PaymentMode = "Driver", ExpenseDate = new DateOnly(2026, 5, 10), CreatedAt = DateTime.UtcNow };
        ctx.TripExpenses.Add(e);
        await ctx.SaveChangesAsync();

        await engine.PostFromSourceAsync(AutoPostSourceTypes.TripExpense, e.Id);

        var v = await LiveVoucherAsync(ctx, AutoPostSourceTypes.TripExpense, e.Id);
        AssertBalanced(v);
        var drv = await ctx.Accounts.FirstAsync(a => a.SourcePartyId == 7 && a.Code == "DRV-7");
        Assert.Equal(200m, v.Lines.First(l => l.AccountId == drv.Id && l.DrCr == DrCr.CR).Amount);
    }

    [Fact]
    public async Task LorryHireMemo_Posts_Balanced_Purchase()
    {
        using var ctx = NewContext();
        var engine = NewEngine(ctx);
        var c = new LorryHireContract
        {
            ContractNo = "LHS-1", ContractDate = new DateOnly(2026, 5, 10), VendorId = 3, VendorName = "Mahadev Transport",
            TotalHireAmount = 10000m, AdvanceCashPaid = 2000m, DieselAdvanceAmount = 1000m, TdsAmount = 100m,
            OtherDeductions = 0m, BalancePayable = 6900m, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.LorryHireContracts.Add(c);
        await ctx.SaveChangesAsync();

        await engine.PostFromSourceAsync(AutoPostSourceTypes.LorryHireContract, c.Id);

        var v = await LiveVoucherAsync(ctx, AutoPostSourceTypes.LorryHireContract, c.Id);
        Assert.Equal(VoucherType.Purchase, v.VoucherType);
        AssertBalanced(v);
        var expense = await ctx.Accounts.FirstAsync(a => a.Code == WellKnownLedgers.LorryHireExpense);
        Assert.Equal(10000m, v.Lines.First(l => l.AccountId == expense.Id && l.DrCr == DrCr.DR).Amount);
        Assert.Contains(await ctx.Accounts.ToListAsync(), a => a.SourcePartyId == 3 && a.Code == "CR-3");
    }

    [Fact]
    public async Task LorryHirePayment_Posts_Balanced_Payment()
    {
        using var ctx = NewContext();
        var engine = NewEngine(ctx);
        var c = new LorryHireContract { ContractNo = "LHS-2", ContractDate = new DateOnly(2026, 5, 10), VendorId = 3, VendorName = "Mahadev", TotalHireAmount = 10000m, BalancePayable = 6900m, PaidBalanceAmount = 3000m, IsActive = true, CreatedAt = DateTime.UtcNow };
        ctx.LorryHireContracts.Add(c);
        await ctx.SaveChangesAsync();

        await engine.PostFromSourceAsync(AutoPostSourceTypes.LorryHirePayment, c.Id);

        var v = await LiveVoucherAsync(ctx, AutoPostSourceTypes.LorryHirePayment, c.Id);
        Assert.Equal(VoucherType.Payment, v.VoucherType);
        AssertBalanced(v);
    }

    [Fact]
    public async Task DriverAdvance_And_Recovery_Post_Balanced()
    {
        using var ctx = NewContext();
        var engine = NewEngine(ctx);
        var adv = new DriverLedgerEntry { DriverId = 7, DriverName = "Ravi", EntryDate = new DateOnly(2026, 5, 10), IsAdvance = true, Amount = 500m, IsActive = true, CreatedAt = DateTime.UtcNow };
        var rec = new DriverLedgerEntry { DriverId = 7, DriverName = "Ravi", EntryDate = new DateOnly(2026, 5, 12), IsAdvance = false, Amount = 200m, IsActive = true, CreatedAt = DateTime.UtcNow };
        ctx.DriverLedgerEntries.AddRange(adv, rec);
        await ctx.SaveChangesAsync();

        await engine.PostFromSourceAsync(AutoPostSourceTypes.DriverLedgerEntry, adv.Id);
        await engine.PostFromSourceAsync(AutoPostSourceTypes.DriverLedgerEntry, rec.Id);

        var va = await LiveVoucherAsync(ctx, AutoPostSourceTypes.DriverLedgerEntry, adv.Id);
        var vr = await LiveVoucherAsync(ctx, AutoPostSourceTypes.DriverLedgerEntry, rec.Id);
        Assert.Equal(VoucherType.Payment, va.VoucherType);
        Assert.Equal(VoucherType.Receipt, vr.VoucherType);
        AssertBalanced(va);
        AssertBalanced(vr);
    }

    // ---------------- idempotency / reverse+repost ----------------

    [Fact]
    public async Task Double_Dispatch_Posts_At_Most_One_Live_Voucher()
    {
        using var ctx = NewContext();
        var engine = NewEngine(ctx);
        var inv = new Invoice { InvoiceNo = "INV-3", InvoiceDate = new DateOnly(2026, 5, 10), SubTotal = 1000m, GrandTotal = 1000m, IsActive = true, CreatedAt = DateTime.UtcNow };
        ctx.Invoices.Add(inv);
        await ctx.SaveChangesAsync();

        await engine.PostFromSourceAsync(AutoPostSourceTypes.Invoice, inv.Id);
        await engine.PostFromSourceAsync(AutoPostSourceTypes.Invoice, inv.Id); // idempotent

        var live = await ctx.Vouchers.CountAsync(v => v.SourceType == AutoPostSourceTypes.Invoice && v.SourceId == inv.Id
            && v.ReversalOfId == null && v.Status == VoucherStatus.Posted);
        Assert.Equal(1, live);
    }

    [Fact]
    public async Task Repost_Reverses_Old_And_Posts_New_From_Current_State()
    {
        using var ctx = NewContext();
        var engine = NewEngine(ctx);
        var inv = new Invoice { InvoiceNo = "INV-4", InvoiceDate = new DateOnly(2026, 5, 10), SubTotal = 1000m, GrandTotal = 1000m, IsActive = true, CreatedAt = DateTime.UtcNow };
        ctx.Invoices.Add(inv);
        await ctx.SaveChangesAsync();
        await engine.PostFromSourceAsync(AutoPostSourceTypes.Invoice, inv.Id);
        var firstLive = await LiveVoucherAsync(ctx, AutoPostSourceTypes.Invoice, inv.Id);

        // Edit the invoice then repost.
        inv.SubTotal = 2000m; inv.GrandTotal = 2000m;
        await ctx.SaveChangesAsync();
        await engine.RepostFromSourceAsync(AutoPostSourceTypes.Invoice, inv.Id);

        var newLive = await LiveVoucherAsync(ctx, AutoPostSourceTypes.Invoice, inv.Id);
        Assert.NotEqual(firstLive.Id, newLive.Id);
        AssertBalanced(newLive);
        Assert.Equal(2000m, newLive.Lines.Where(l => l.DrCr == DrCr.DR).Sum(l => l.Amount));

        // Original is Reversed; a reversal voucher (no source) exists.
        var original = await ctx.Vouchers.FirstAsync(v => v.Id == firstLive.Id);
        Assert.Equal(VoucherStatus.Reversed, original.Status);
        Assert.Contains(await ctx.Vouchers.ToListAsync(), v => v.ReversalOfId == firstLive.Id && v.SourceType == null);

        // Exactly one live voucher for the source.
        Assert.Equal(1, await ctx.Vouchers.CountAsync(v => v.SourceType == AutoPostSourceTypes.Invoice && v.SourceId == inv.Id
            && v.ReversalOfId == null && v.Status == VoucherStatus.Posted));
    }

    [Fact]
    public async Task ReverseFromSource_Reverses_Live_And_Does_Not_Repost()
    {
        using var ctx = NewContext();
        var engine = NewEngine(ctx);
        var inv = new Invoice { InvoiceNo = "INV-5", InvoiceDate = new DateOnly(2026, 5, 10), SubTotal = 1000m, GrandTotal = 1000m, IsActive = true, CreatedAt = DateTime.UtcNow };
        ctx.Invoices.Add(inv);
        await ctx.SaveChangesAsync();
        await engine.PostFromSourceAsync(AutoPostSourceTypes.Invoice, inv.Id);

        await engine.ReverseFromSourceAsync(AutoPostSourceTypes.Invoice, inv.Id);

        Assert.Null(await engine.GetLiveVoucherIdAsync(AutoPostSourceTypes.Invoice, inv.Id));
    }

    // ---------------- dispatcher gating ----------------

    [Fact]
    public async Task DefaultOff_Drain_Skips_And_Leaves_Rows_Pending()
    {
        using var ctx = NewContext();
        var engine = NewEngine(ctx);
        var inv = new Invoice { InvoiceNo = "INV-6", InvoiceDate = new DateOnly(2026, 5, 10), SubTotal = 1000m, GrandTotal = 1000m, IsActive = true, CreatedAt = DateTime.UtcNow };
        ctx.Invoices.Add(inv);
        await ctx.SaveChangesAsync();
        await engine.EnqueueAsync(AutoPostSourceTypes.Invoice, inv.Id, AutoPostOperation.Post);

        var posted = await engine.DrainPendingForCurrentTenantAsync(maxAttempts: 5);

        Assert.Equal(0, posted); // default OFF -> skipped
        var row = await ctx.AutoPostingQueue.FirstAsync();
        Assert.Equal(AutoPostStatus.Pending, row.Status);
        Assert.Equal(0, await ctx.Vouchers.CountAsync());
    }

    [Fact]
    public async Task Enabled_Drain_Posts_And_Marks_Done()
    {
        using var ctx = NewContext();
        var engine = NewEngine(ctx);
        await EnableAsync(ctx);
        var inv = new Invoice { InvoiceNo = "INV-7", InvoiceDate = new DateOnly(2026, 5, 10), SubTotal = 1000m, GrandTotal = 1000m, IsActive = true, CreatedAt = DateTime.UtcNow };
        ctx.Invoices.Add(inv);
        await ctx.SaveChangesAsync();
        await engine.EnqueueAsync(AutoPostSourceTypes.Invoice, inv.Id, AutoPostOperation.Post);

        var posted = await engine.DrainPendingForCurrentTenantAsync(maxAttempts: 5);

        Assert.Equal(1, posted);
        Assert.Equal(AutoPostStatus.Done, (await ctx.AutoPostingQueue.FirstAsync()).Status);
        Assert.NotNull(await engine.GetLiveVoucherIdAsync(AutoPostSourceTypes.Invoice, inv.Id));
    }

    [Fact]
    public async Task ClosedPeriod_Drain_Marks_Row_Deferred_And_Posts_Nothing()
    {
        using var ctx = NewContext();
        var engine = NewEngine(ctx);
        await EnableAsync(ctx);
        var periods = new FinancialPeriodService(ctx);
        var p = await periods.CreateAsync(new CreatePeriodRequest { FyLabel = "2026-27" });
        await periods.CloseAsync(p.Id);

        var inv = new Invoice { InvoiceNo = "INV-8", InvoiceDate = new DateOnly(2026, 6, 1), SubTotal = 1000m, GrandTotal = 1000m, IsActive = true, CreatedAt = DateTime.UtcNow };
        ctx.Invoices.Add(inv);
        await ctx.SaveChangesAsync();
        await engine.EnqueueAsync(AutoPostSourceTypes.Invoice, inv.Id, AutoPostOperation.Post);

        var posted = await engine.DrainPendingForCurrentTenantAsync(maxAttempts: 5);

        Assert.Equal(0, posted);
        var row = await ctx.AutoPostingQueue.FirstAsync();
        Assert.Equal(AutoPostStatus.Deferred, row.Status);
        Assert.Null(await engine.GetLiveVoucherIdAsync(AutoPostSourceTypes.Invoice, inv.Id));
    }
}
