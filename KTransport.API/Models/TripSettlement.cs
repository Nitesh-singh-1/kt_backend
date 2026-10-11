using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// Append-only child of <see cref="Trip"/>. One row per settlement event.
    /// Introduced by TASK-037 — first concrete application of ADR
    /// `database-table-hierarchy.md` (settlement is a lifecycle event, not a
    /// column on the booking aggregate root). Snapshot columns are frozen at
    /// write time and MUST NOT be recomputed on read.
    /// </summary>
    public class TripSettlement : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public long TripId { get; set; }

        public virtual Trip? Trip { get; set; }

        public string SettlementNo { get; set; } = null!; // TRIP-SET-YYYY-NNNN per tenant

        public DateOnly SettlementDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        public decimal EndOdometer { get; set; } = 0;

        public decimal TotalKilometers { get; set; } = 0; // Snapshot: EndOdometer - StartOdometer at write time

        // Section A — Driver accountability snapshot
        public decimal DriverAdvanceCashSnapshot { get; set; } = 0;

        public decimal DriverAdvanceFuelSnapshot { get; set; } = 0;

        public decimal CollectedToPayFreight { get; set; } = 0;

        public decimal TotalDriverAccountability { get; set; } = 0; // AdvCash + AdvFuel + CollectedToPay, persisted for audit

        // Section B — Expenses snapshot at settlement time
        public decimal TotalExpensesSnapshot { get; set; } = 0;

        // Section C — Reconciliation outcome
        public decimal NetDriverBalance { get; set; } = 0; // Positive = driver owes office; Negative = office owes driver

        public decimal SettledAmount { get; set; } = 0; // Actual cash/transfer moved

        public string PaymentMode { get; set; } = "CASH"; // CASH, UPI, BANK_TRANSFER, CHEQUE

        public string? PaymentReference { get; set; }

        public string? SettlementRemarks { get; set; }

        // Reversal / audit — append-only guarantee
        public bool IsReversed { get; set; } = false;

        public DateTime? ReversedAt { get; set; }

        public int? ReversedBy { get; set; }

        public string? ReversalReason { get; set; }

        // Audit
        public int? SettledBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
