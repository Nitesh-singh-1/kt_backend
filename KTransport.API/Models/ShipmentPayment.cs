using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// Append-only ledger of money received against a bilty/GR. One row per payment event
    /// across all flows (receipt / on-spot / bill book / delivery settlement). The shipment's
    /// PaidAmount/DueAmount are the sum of the non-reversed rows here — this table is the source
    /// of truth for payment history and the only thing that makes a single partial payment
    /// reversible. Rows are never hard-updated for money; a reversal flips <see cref="IsReversed"/>.
    /// </summary>
    public class ShipmentPayment : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public long ShipmentId { get; set; }

        public virtual Shipment? Shipment { get; set; }

        public decimal Amount { get; set; }

        public string? PaymentMode { get; set; }

        public string? ReferenceNo { get; set; }

        public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        public PaymentSource Source { get; set; }

        /// <summary>Set when the payment came through a Bill Book invoice.</summary>
        public long? InvoiceId { get; set; }

        // ---- Reversal (append-only semantics: a reversal flips these, never deletes the row) ----
        public bool IsReversed { get; set; } = false;

        public DateTime? ReversedAt { get; set; }

        public int? ReversedByUserId { get; set; }

        public string? ReversalReason { get; set; }

        public int? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
