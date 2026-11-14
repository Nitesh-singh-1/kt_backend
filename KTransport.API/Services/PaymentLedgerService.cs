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
    public class PaymentLedgerService : IPaymentLedgerService
    {
        private readonly KTransportDbContext _context;
        private readonly IAutoPostingService? _autoPosting;

        public PaymentLedgerService(KTransportDbContext context, IAutoPostingService? autoPosting = null)
        {
            _context = context;
            _autoPosting = autoPosting;
        }

        private static (string Label, bool ReversibleHere) SourceMeta(PaymentSource s) => s switch
        {
            PaymentSource.Receipt => ("Money Receipt", true),
            PaymentSource.OnSpot => ("On-spot (booking)", true),
            PaymentSource.DeliverySettlement => ("Delivery Settlement", true),
            PaymentSource.OpeningBackfill => ("Opening Balance", true),
            PaymentSource.BillBook => ("Bill Book", false),
            _ => (s.ToString(), false),
        };

        public async Task<List<ShipmentPaymentDto>> GetByShipmentAsync(long shipmentId)
        {
            var rows = await _context.ShipmentPayments
                .Where(p => p.ShipmentId == shipmentId)
                .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
                .ToListAsync();

            return rows.Select(p =>
            {
                var (label, reversible) = SourceMeta(p.Source);
                return new ShipmentPaymentDto
                {
                    Id = p.Id,
                    ShipmentId = p.ShipmentId,
                    Amount = p.Amount,
                    PaymentMode = p.PaymentMode,
                    ReferenceNo = p.ReferenceNo,
                    PaymentDate = p.PaymentDate,
                    Source = p.Source.ToString(),
                    SourceLabel = label,
                    CanReverseHere = reversible && !p.IsReversed,
                    InvoiceId = p.InvoiceId,
                    IsReversed = p.IsReversed,
                    ReversedAt = p.ReversedAt,
                    ReversalReason = p.ReversalReason,
                    CreatedAt = p.CreatedAt,
                };
            }).ToList();
        }

        public async Task<(bool Success, string Message)> ReverseAsync(long paymentId, string? reason, int userId)
        {
            var payment = await _context.ShipmentPayments.FirstOrDefaultAsync(p => p.Id == paymentId);
            if (payment == null) return (false, "Payment not found.");
            if (payment.IsReversed) return (false, "This payment has already been reversed.");

            if (payment.Source == PaymentSource.BillBook)
            {
                return (false, "This payment was received through a Bill Book invoice. Reverse it by voiding or editing that bill, not from the bilty.");
            }

            var shipment = await _context.Shipments.FirstOrDefaultAsync(s => s.Id == payment.ShipmentId);
            if (shipment == null) return (false, "Bilty not found for this payment.");

            // 1) Flip the ledger row (append-only: never delete money history).
            payment.IsReversed = true;
            payment.ReversedAt = DateTime.UtcNow;
            payment.ReversedByUserId = userId;
            payment.ReversalReason = string.IsNullOrWhiteSpace(reason) ? "Payment reversed" : reason!.Trim();

            // 2) Recompute the bilty's money from the remaining ACTIVE rows (full restore — a
            //    reversed settlement's discount is undone, so due goes back to grand total minus
            //    the cash still on the ledger).
            // Exclude the row we just flipped by id — its IsReversed change isn't saved yet, so a
            // DB-side SUM would still count it.
            var activePaid = await _context.ShipmentPayments
                .Where(p => p.ShipmentId == shipment.Id && !p.IsReversed && p.Id != payment.Id)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            var grandTotal = shipment.GrandTotal > 0 ? shipment.GrandTotal : shipment.TotalFreight;
            shipment.PaidAmount = activePaid;
            shipment.DueAmount = Math.Max(0, grandTotal - activePaid);
            shipment.SettledDiscountAmount = 0;      // full restore: discount undone
            shipment.DiscountReason = null;
            shipment.SettledReceivedAmount = activePaid;
            shipment.IsSettled = grandTotal > 0 && activePaid >= grandTotal;
            shipment.IsPartialPayment = activePaid > 0 && activePaid < grandTotal;
            if (activePaid == 0)
            {
                shipment.SettledPaymentMode = null;
                shipment.SettlementReferenceNo = null;
            }
            shipment.UpdatedAt = DateTime.UtcNow;
            shipment.UpdatedBy = userId;

            var (label, _) = SourceMeta(payment.Source);
            _context.ShipmentStatusHistories.Add(new ShipmentStatusHistory
            {
                TenantId = shipment.TenantId,
                ShipmentId = shipment.Id,
                FromStatus = shipment.Status,
                ToStatus = shipment.Status,
                Location = shipment.ToLocation ?? "—",
                Remarks = $"Payment reversed: ₹{payment.Amount:N2} ({label}). Reason: {payment.ReversalReason}",
                ChangedByUserId = userId,
                ChangedAt = DateTime.UtcNow,
            });

            await _context.SaveChangesAsync();

            // 3) Re-post the accounting voucher off the new (reduced) paid amount.
            if (_autoPosting != null)
                await _autoPosting.EnqueueAsync(AutoPostSourceTypes.Shipment, shipment.Id, AutoPostOperation.Repost);

            var unlocked = shipment.PaidAmount <= 0;
            return (true, unlocked
                ? "Payment reversed. The bilty is now fully unpaid and can be edited again."
                : $"Payment reversed. ₹{shipment.PaidAmount:N2} is still received against this bilty, so it stays locked for editing.");
        }
    }
}
