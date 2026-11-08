using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// One debit or credit line of a <see cref="Voucher"/> (append-only child; a voucher has
    /// ≥ 2 lines). The side is carried by <see cref="DrCr"/>; <see cref="Amount"/> is always &gt; 0.
    /// The per-voucher balancing invariant (ΣDR = ΣCR) is enforced in VoucherService and by a
    /// database trigger.
    /// </summary>
    public class VoucherLine : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public long VoucherId { get; set; }

        public virtual Voucher? Voucher { get; set; }

        public long AccountId { get; set; }

        public virtual Account? Account { get; set; }

        public DrCr DrCr { get; set; }

        public decimal Amount { get; set; }

        public string? LineNarration { get; set; }
    }
}
