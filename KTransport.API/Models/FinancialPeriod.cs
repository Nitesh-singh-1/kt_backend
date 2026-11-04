using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// An Indian financial year (1 Apr – 31 Mar) for a tenant. Once <see cref="Status"/> is
    /// Closed, posting a voucher dated inside the period is rejected (back-dated posting lock).
    /// </summary>
    public class FinancialPeriod : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        /// <summary>e.g. "2026-27".</summary>
        public string FyLabel { get; set; } = null!;

        public DateOnly StartDate { get; set; }

        public DateOnly EndDate { get; set; }

        public PeriodStatus Status { get; set; } = PeriodStatus.Open;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
