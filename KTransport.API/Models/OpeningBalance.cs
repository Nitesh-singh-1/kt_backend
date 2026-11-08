using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// Per-account, per-financial-year opening figure. This is the migration path for existing
    /// tenants (ADR decision: opening balances now, full historical backfill opt-in later) —
    /// the ledger is correct going forward without replaying every historical document.
    /// </summary>
    public class OpeningBalance : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public long AccountId { get; set; }

        public virtual Account? Account { get; set; }

        public long PeriodId { get; set; }

        public virtual FinancialPeriod? Period { get; set; }

        public DrCr DrCr { get; set; }

        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
