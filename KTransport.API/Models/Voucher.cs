using System;
using System.Collections.Generic;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// A double-entry voucher HEADER (aggregate root, ≤ 20 columns per DATABASE.md).
    /// Carries NO amount column — the amount is derived from <see cref="Lines"/> so it can
    /// never drift. A Posted voucher is immutable (DATABASE.md rule 4); a correction is a
    /// Reversed mirror voucher that points back via <see cref="ReversalOfId"/>.
    /// </summary>
    public class Voucher : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public VoucherType VoucherType { get; set; }

        /// <summary>Human-readable, unique per tenant+type, e.g. JV-2026-27-0001.</summary>
        public string VoucherNo { get; set; } = null!;

        public DateOnly Date { get; set; }

        public long PeriodId { get; set; }

        public virtual FinancialPeriod? Period { get; set; }

        public string? Narration { get; set; }

        public VoucherStatus Status { get; set; } = VoucherStatus.Posted;

        /// <summary>Phase 2: operational source document type (e.g. "Invoice").</summary>
        public string? SourceType { get; set; }

        /// <summary>Phase 2: operational source document id.</summary>
        public long? SourceId { get; set; }

        /// <summary>Set on a reversal voucher — the original voucher it reverses.</summary>
        public long? ReversalOfId { get; set; }

        public virtual Voucher? ReversalOf { get; set; }

        public int? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<VoucherLine> Lines { get; set; } = new List<VoucherLine>();
    }
}
