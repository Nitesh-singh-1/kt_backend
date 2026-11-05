using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KTransport.API.Services
{
    /// <summary>
    /// Phase 2 auto-posting engine. See <see cref="IAutoPostingService"/>. Every builder produces a
    /// voucher that balances BY CONSTRUCTION (ADR Q3). All statutory values stay DATA (TDS from the
    /// contract, GST from the invoice via <see cref="IGstPostingResolver"/>).
    /// </summary>
    public class AutoPostingService : IAutoPostingService
    {
        private readonly KTransportDbContext _context;
        private readonly IVoucherService _vouchers;
        private readonly IAccountService _accounts;
        private readonly IGstPostingResolver _gst;
        private readonly ILogger<AutoPostingService> _logger;

        public AutoPostingService(
            KTransportDbContext context,
            IVoucherService vouchers,
            IAccountService accounts,
            IGstPostingResolver gst,
            ILogger<AutoPostingService> logger)
        {
            _context = context;
            _vouchers = vouchers;
            _accounts = accounts;
            _gst = gst;
            _logger = logger;
        }

        // ---------------- hook (outbox) ----------------

        public async Task EnqueueAsync(string sourceType, long sourceId, AutoPostOperation op)
        {
            // GUARDED: a hook/accounting failure must NEVER break the operational write (that is the
            // whole point of the outbox). The row is persisted immediately after the operational
            // write so the intent is durable; the dispatcher does the actual posting after commit.
            try
            {
                _context.AutoPostingQueue.Add(new AutoPostingQueueRow
                {
                    SourceType = sourceType,
                    SourceId = sourceId,
                    Operation = op,
                    Status = AutoPostStatus.Pending,
                    EnqueuedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Auto-posting enqueue failed for {SourceType}/{SourceId} ({Op}); operational write is unaffected.",
                    sourceType, sourceId, op);
            }
        }

        // ---------------- engine ----------------

        public async Task PostFromSourceAsync(string sourceType, long sourceId)
        {
            await EnsureWellKnownLedgersAsync();

            // Idempotency pre-check (the partial unique index is the race backstop).
            if (await GetLiveVoucherIdAsync(sourceType, sourceId) is not null)
                return;

            var draft = await BuildDraftAsync(sourceType, sourceId);
            if (draft == null || draft.Lines.Count < 2)
                return; // nothing accountable to post (e.g. zero amount)

            await _vouchers.CreateFromSourceAsync(
                draft.Type, draft.Date, draft.Narration, draft.Lines, sourceType, sourceId, userId: null);
        }

        public async Task RepostFromSourceAsync(string sourceType, long sourceId)
        {
            var liveId = await GetLiveVoucherIdAsync(sourceType, sourceId);
            if (liveId is not null)
                await _vouchers.ReverseAsync(liveId.Value, new ReverseVoucherRequest(), userId: null);

            await PostFromSourceAsync(sourceType, sourceId);
        }

        public async Task ReverseFromSourceAsync(string sourceType, long sourceId)
        {
            var liveId = await GetLiveVoucherIdAsync(sourceType, sourceId);
            if (liveId is not null)
                await _vouchers.ReverseAsync(liveId.Value, new ReverseVoucherRequest(), userId: null);
        }

        public async Task<long?> GetLiveVoucherIdAsync(string sourceType, long sourceId)
        {
            return await _context.Vouchers
                .Where(v => v.SourceType == sourceType && v.SourceId == sourceId
                    && v.ReversalOfId == null && v.Status == VoucherStatus.Posted)
                .Select(v => (long?)v.Id)
                .FirstOrDefaultAsync();
        }

        // ---------------- dispatcher worker body (per tenant) ----------------

        public async Task<bool> IsAutoPostingEnabledAsync()
        {
            var s = await _context.AccountingSettings.AsNoTracking().FirstOrDefaultAsync();
            return s?.AutoPostingEnabled ?? false;
        }

        public async Task<int> DrainPendingForCurrentTenantAsync(int maxAttempts, CancellationToken cancellationToken = default)
        {
            if (!await IsAutoPostingEnabledAsync())
                return 0; // ADR Q6: disabled tenant -> skip, leave rows Pending for a later enable.

            await EnsureWellKnownLedgersAsync();

            var pending = await _context.AutoPostingQueue
                .Where(r => r.Status == AutoPostStatus.Pending)
                .OrderBy(r => r.EnqueuedAt).ThenBy(r => r.Id)
                .ToListAsync(cancellationToken);

            var processed = 0;
            foreach (var row in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    switch (row.Operation)
                    {
                        case AutoPostOperation.Post: await PostFromSourceAsync(row.SourceType, row.SourceId); break;
                        case AutoPostOperation.Repost: await RepostFromSourceAsync(row.SourceType, row.SourceId); break;
                        case AutoPostOperation.Reverse: await ReverseFromSourceAsync(row.SourceType, row.SourceId); break;
                    }
                    row.Status = AutoPostStatus.Done;
                    row.ProcessedAt = DateTime.UtcNow;
                    row.LastError = null;
                    processed++;
                }
                catch (AccountingException ax)
                {
                    if (IsClosedPeriod(ax))
                    {
                        // ADR Q5: closed period -> park for review; never block, never reslot.
                        row.Status = AutoPostStatus.Deferred;
                        row.LastError = ax.Message;
                    }
                    else
                    {
                        // Business-rule failure (unbalanced / missing ledger / bad data) — retry won't help.
                        row.Attempts += 1;
                        row.Status = AutoPostStatus.Failed;
                        row.LastError = ax.Message;
                    }
                }
                catch (Exception ex)
                {
                    // Transient (DB / concurrency). Retry until the cap.
                    row.Attempts += 1;
                    row.LastError = ex.Message;
                    row.Status = row.Attempts >= maxAttempts ? AutoPostStatus.Failed : AutoPostStatus.Pending;
                    _logger.LogWarning(ex, "Auto-posting attempt {Attempts} failed for {SourceType}/{SourceId}.",
                        row.Attempts, row.SourceType, row.SourceId);
                }

                await _context.SaveChangesAsync(cancellationToken);
            }

            return processed;
        }

        private static bool IsClosedPeriod(AccountingException ax)
            => ax.Message.Contains("closed", StringComparison.OrdinalIgnoreCase);

        // ---------------- well-known ledger seeding ----------------

        public async Task EnsureWellKnownLedgersAsync()
        {
            await _accounts.EnsureSeededAsync(); // the 28 Tally groups must exist first.

            var groups = await _context.Accounts
                .Where(a => a.IsGroup)
                .ToDictionaryAsync(a => a.Code, a => a);
            var existingCodes = (await _context.Accounts.Select(a => a.Code).ToListAsync()).ToHashSet();

            var toAdd = new List<Account>();
            foreach (var wk in WellKnownLedgers.All)
            {
                if (existingCodes.Contains(wk.Key)) continue;
                if (!groups.TryGetValue(wk.ParentGroupCode, out var parent)) continue;

                toAdd.Add(new Account
                {
                    ParentId = parent.Id,
                    Code = wk.Key,
                    Name = wk.Name,
                    AccountType = parent.AccountType,
                    Nature = parent.Nature,
                    IsGroup = wk.IsGroup,
                    IsSystem = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (toAdd.Count > 0)
            {
                _context.Accounts.AddRange(toAdd);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Seeded {Count} well-known accounting ledgers.", toAdd.Count);
            }
        }

        // ---------------- account resolvers ----------------

        private async Task<long> ResolveWellKnownAsync(string key)
        {
            var id = await _context.Accounts
                .Where(a => a.Code == key && a.IsSystem && !a.IsGroup)
                .Select(a => (long?)a.Id)
                .FirstOrDefaultAsync();
            if (id is null)
                throw AccountingException.Unprocessable($"Well-known ledger '{key}' is not seeded for this tenant.");
            return id.Value;
        }

        /// <summary>Customer A/R: PartyId sub-ledger under Sundry Debtors (1160), else WALKIN_DEBTOR.</summary>
        private async Task<long> ResolveDebtorAsync(long? partyId, string? name)
        {
            if (partyId is null)
                return await ResolveWellKnownAsync(WellKnownLedgers.WalkinDebtor);
            return await FindOrCreateSubLedgerAsync("1160", partyId, name ?? $"Customer {partyId}", $"DR-{partyId}");
        }

        /// <summary>Vendor A/P: VendorId sub-ledger under Sundry Creditors (2030).</summary>
        private async Task<long> ResolveCreditorAsync(long? vendorId, string? name)
        {
            if (vendorId is null)
                throw AccountingException.Unprocessable("Vendor A/P sub-ledger requires a VendorId on the hire memo.");
            return await FindOrCreateSubLedgerAsync("2030", vendorId, name ?? $"Vendor {vendorId}", $"CR-{vendorId}");
        }

        /// <summary>Driver advance sub-ledger under the DRIVER_ADVANCES group.</summary>
        private async Task<long> ResolveDriverAdvanceAsync(long? driverId, string name)
        {
            var group = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == WellKnownLedgers.DriverAdvances && a.IsGroup)
                ?? throw AccountingException.Unprocessable("DRIVER_ADVANCES group is not seeded for this tenant.");

            Account? acc;
            if (driverId.HasValue)
                acc = await _context.Accounts.FirstOrDefaultAsync(a => a.ParentId == group.Id && a.SourcePartyId == driverId.Value);
            else
                acc = await _context.Accounts.FirstOrDefaultAsync(a => a.ParentId == group.Id && a.SourcePartyId == null && a.Name == name);

            if (acc != null) return acc.Id;

            var code = driverId.HasValue
                ? await UniqueCodeAsync($"DRV-{driverId}")
                : await UniqueCodeAsync($"DRV-{Math.Abs(name.GetHashCode()) % 100000}");

            acc = new Account
            {
                ParentId = group.Id,
                Code = code,
                Name = name,
                AccountType = group.AccountType,
                Nature = group.Nature,
                IsGroup = false,
                IsSystem = false,
                SourcePartyId = driverId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Accounts.Add(acc);
            await _context.SaveChangesAsync();
            return acc.Id;
        }

        private async Task<long> FindOrCreateSubLedgerAsync(string parentGroupCode, long? sourcePartyId, string name, string preferredCode)
        {
            var parent = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == parentGroupCode && a.IsGroup)
                ?? throw AccountingException.Unprocessable($"Parent group '{parentGroupCode}' is not seeded for this tenant.");

            var existing = await _context.Accounts
                .FirstOrDefaultAsync(a => a.ParentId == parent.Id && a.SourcePartyId == sourcePartyId);
            if (existing != null) return existing.Id;

            var acc = new Account
            {
                ParentId = parent.Id,
                Code = await UniqueCodeAsync(preferredCode),
                Name = name,
                AccountType = parent.AccountType,
                Nature = parent.Nature,
                IsGroup = false,
                IsSystem = false,
                SourcePartyId = sourcePartyId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Accounts.Add(acc);
            await _context.SaveChangesAsync();
            return acc.Id;
        }

        private async Task<string> UniqueCodeAsync(string preferred)
        {
            if (!await _context.Accounts.AnyAsync(a => a.Code == preferred)) return preferred;
            for (var n = 2; ; n++)
            {
                var candidate = $"{preferred}-{n}";
                if (!await _context.Accounts.AnyAsync(a => a.Code == candidate)) return candidate;
            }
        }

        // ---------------- voucher drafts (one per source; balance by construction) ----------------

        private sealed record VoucherDraft(VoucherType Type, DateOnly Date, string? Narration, List<VoucherLineInput> Lines);

        private static decimal R(decimal v) => Math.Round(v, 2);

        private static VoucherLineInput Line(long accountId, DrCr side, decimal amount, string? narr = null)
            => new() { AccountId = accountId, DrCr = side, Amount = R(amount), LineNarration = narr };

        private Task<VoucherDraft?> BuildDraftAsync(string sourceType, long sourceId) => sourceType switch
        {
            AutoPostSourceTypes.Invoice => BuildInvoiceSalesAsync(sourceId),
            AutoPostSourceTypes.InvoicePayment => BuildInvoicePaymentAsync(sourceId),
            AutoPostSourceTypes.Shipment => BuildShipmentReceiptAsync(sourceId),
            AutoPostSourceTypes.TripExpense => BuildTripExpenseAsync(sourceId),
            AutoPostSourceTypes.LorryHireContract => BuildLorryHireMemoAsync(sourceId),
            AutoPostSourceTypes.LorryHirePayment => BuildLorryHirePaymentAsync(sourceId),
            AutoPostSourceTypes.DriverLedgerEntry => BuildDriverLedgerAsync(sourceId),
            _ => throw AccountingException.Unprocessable($"Unknown auto-posting source type '{sourceType}'.")
        };

        // 1. Freight invoice -> Sales.
        private async Task<VoucherDraft?> BuildInvoiceSalesAsync(long invoiceId)
        {
            var inv = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (inv == null || !inv.IsActive) return null;

            var gstLegs = _gst.ResolveOutputGst(inv);
            var subTotal = R(inv.SubTotal);
            var other = R(inv.OtherCharges);
            var discount = R(inv.Discount);
            var gstTotal = gstLegs.Sum(l => l.Amount);
            var creditTotal = subTotal + gstTotal + other;
            var arDebit = creditTotal - discount;

            var lines = new List<VoucherLineInput>();
            if (arDebit > 0)
                lines.Add(Line(await ResolveDebtorAsync(inv.PartyId, inv.PartyName), DrCr.DR, arDebit, "Accounts receivable"));
            if (discount > 0)
                lines.Add(Line(await ResolveWellKnownAsync(WellKnownLedgers.DiscountAllowed), DrCr.DR, discount, "Discount allowed"));
            if (subTotal > 0)
                lines.Add(Line(await ResolveWellKnownAsync(WellKnownLedgers.FreightIncome), DrCr.CR, subTotal, "Freight income"));
            foreach (var leg in gstLegs)
                lines.Add(Line(await ResolveWellKnownAsync(leg.LedgerKey), DrCr.CR, leg.Amount, "Output GST"));
            if (other > 0)
                lines.Add(Line(await ResolveWellKnownAsync(WellKnownLedgers.OtherChargesIncome), DrCr.CR, other, "Other charges"));

            return new VoucherDraft(VoucherType.Sales, inv.InvoiceDate, $"Auto: Sales for invoice {inv.InvoiceNo}", lines);
        }

        // 2b. Invoice payment -> Receipt (cumulative PaidAmount; source_id = Invoice.Id).
        private async Task<VoucherDraft?> BuildInvoicePaymentAsync(long invoiceId)
        {
            var inv = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (inv == null) return null;
            var paid = R(inv.PaidAmount);
            if (paid <= 0) return null;

            WellKnownLedgers.TryMapPaymentModeToLedger(inv.PaymentMode, out var cashKey);
            var lines = new List<VoucherLineInput>
            {
                Line(await ResolveWellKnownAsync(cashKey), DrCr.DR, paid, "Cash / bank received"),
                Line(await ResolveDebtorAsync(inv.PartyId, inv.PartyName), DrCr.CR, paid, "Against receivable"),
            };
            return new VoucherDraft(VoucherType.Receipt, inv.InvoiceDate, $"Auto: Receipt against invoice {inv.InvoiceNo}", lines);
        }

        // 2. Money receipt / shipment settlement -> Receipt.
        private async Task<VoucherDraft?> BuildShipmentReceiptAsync(long shipmentId)
        {
            var s = await _context.Shipments.FirstOrDefaultAsync(x => x.Id == shipmentId);
            if (s == null) return null;
            var amt = R(s.SettledReceivedAmount ?? s.PaidAmount);
            if (amt <= 0) return null;

            WellKnownLedgers.TryMapPaymentModeToLedger(s.SettledPaymentMode, out var cashKey);
            var lines = new List<VoucherLineInput>
            {
                Line(await ResolveWellKnownAsync(cashKey), DrCr.DR, amt, "Cash / bank received"),
                Line(await ResolveDebtorAsync(s.ConsignorPartyId, s.ConsignorName), DrCr.CR, amt, "Against receivable"),
            };
            var date = s.DeliveryDate ?? s.ShipmentDate;
            return new VoucherDraft(VoucherType.Receipt, date, $"Auto: Receipt for shipment {s.ShipmentNo}", lines);
        }

        // 3. Trip expense -> Payment.
        private async Task<VoucherDraft?> BuildTripExpenseAsync(long expenseId)
        {
            var e = await _context.TripExpenses.FirstOrDefaultAsync(x => x.Id == expenseId);
            if (e == null) return null;
            var amt = R(e.Amount);
            if (amt <= 0) return null;

            var expenseId2 = await ResolveWellKnownAsync(WellKnownLedgers.ForExpenseType(e.ExpenseType));

            long creditId;
            if (!string.IsNullOrWhiteSpace(e.PaymentMode) && e.PaymentMode.Trim().Equals("Driver", StringComparison.OrdinalIgnoreCase))
            {
                // Driver-funded expense credits the driver's advance sub-ledger (ADR Q3 / open item 6).
                var trip = await _context.Trips.FirstOrDefaultAsync(t => t.Id == e.TripId);
                creditId = await ResolveDriverAdvanceAsync(trip?.DriverId, trip?.DriverName ?? e.PaidTo ?? "Driver");
            }
            else
            {
                WellKnownLedgers.TryMapPaymentModeToLedger(e.PaymentMode, out var cashKey);
                creditId = await ResolveWellKnownAsync(cashKey);
            }

            var lines = new List<VoucherLineInput>
            {
                Line(expenseId2, DrCr.DR, amt, e.ExpenseType.ToString()),
                Line(creditId, DrCr.CR, amt, "Paid"),
            };
            return new VoucherDraft(VoucherType.Payment, e.ExpenseDate, $"Auto: Trip expense #{e.Id} ({e.ExpenseType})", lines);
        }

        // 4. Vendor lorry-hire memo -> Purchase.
        private async Task<VoucherDraft?> BuildLorryHireMemoAsync(long contractId)
        {
            var c = await _context.LorryHireContracts.FirstOrDefaultAsync(x => x.Id == contractId);
            if (c == null || !c.IsActive) return null;
            var total = R(c.TotalHireAmount);
            if (total <= 0) return null;

            var lines = new List<VoucherLineInput>
            {
                Line(await ResolveWellKnownAsync(WellKnownLedgers.LorryHireExpense), DrCr.DR, total, "Lorry hire expense"),
            };
            if (c.BalancePayable > 0)
                lines.Add(Line(await ResolveCreditorAsync(c.VendorId, c.VendorName), DrCr.CR, c.BalancePayable, "Vendor payable"));
            if (c.TdsAmount > 0)
                lines.Add(Line(await ResolveWellKnownAsync(WellKnownLedgers.Tds194CPayable), DrCr.CR, c.TdsAmount, "TDS 194C"));
            if (c.AdvanceCashPaid > 0)
                lines.Add(Line(await ResolveWellKnownAsync(WellKnownLedgers.Cash), DrCr.CR, c.AdvanceCashPaid, "Advance cash paid"));
            if (c.DieselAdvanceAmount > 0)
                lines.Add(Line(await ResolveWellKnownAsync(WellKnownLedgers.Cash), DrCr.CR, c.DieselAdvanceAmount, "Diesel advance"));
            if (c.OtherDeductions > 0)
                lines.Add(Line(await ResolveWellKnownAsync(WellKnownLedgers.VendorDeductionsIncome), DrCr.CR, c.OtherDeductions, "Deductions"));

            return new VoucherDraft(VoucherType.Purchase, c.ContractDate, $"Auto: Lorry hire {c.ContractNo}", lines);
        }

        // 4b. Lorry-hire balance payment -> Payment (cumulative; source_id = LorryHireContract.Id).
        private async Task<VoucherDraft?> BuildLorryHirePaymentAsync(long contractId)
        {
            var c = await _context.LorryHireContracts.FirstOrDefaultAsync(x => x.Id == contractId);
            if (c == null) return null;
            var paid = R(c.PaidBalanceAmount);
            if (paid <= 0) return null;

            var lines = new List<VoucherLineInput>
            {
                Line(await ResolveCreditorAsync(c.VendorId, c.VendorName), DrCr.DR, paid, "Vendor payable settled"),
                Line(await ResolveWellKnownAsync(WellKnownLedgers.Cash), DrCr.CR, paid, "Paid"),
            };
            return new VoucherDraft(VoucherType.Payment, c.ContractDate, $"Auto: Lorry hire payment {c.ContractNo}", lines);
        }

        // 5. Driver ledger -> Payment (advance) / Receipt (recovery).
        private async Task<VoucherDraft?> BuildDriverLedgerAsync(long entryId)
        {
            var en = await _context.DriverLedgerEntries.FirstOrDefaultAsync(x => x.Id == entryId);
            if (en == null || !en.IsActive) return null;
            var amt = R(en.Amount);
            if (amt <= 0) return null;

            var drvId = await ResolveDriverAdvanceAsync(en.DriverId, en.DriverName);
            var cashId = await ResolveWellKnownAsync(WellKnownLedgers.Cash);

            List<VoucherLineInput> lines;
            VoucherType type;
            if (en.IsAdvance)
            {
                type = VoucherType.Payment;
                lines = new() { Line(drvId, DrCr.DR, amt, "Driver advance"), Line(cashId, DrCr.CR, amt, "Cash paid") };
            }
            else
            {
                type = VoucherType.Receipt;
                lines = new() { Line(cashId, DrCr.DR, amt, "Cash recovered"), Line(drvId, DrCr.CR, amt, "Driver advance recovery") };
            }
            return new VoucherDraft(type, en.EntryDate, $"Auto: Driver {(en.IsAdvance ? "advance" : "recovery")} #{en.Id}", lines);
        }
    }
}
