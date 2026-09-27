using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>A driver account transaction — advance paid to a driver, or a recovery/settlement against it.</summary>
    public class DriverLedgerEntry : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public long? DriverId { get; set; }

        public virtual Driver? Driver { get; set; }

        public string DriverName { get; set; } = null!;

        public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        /// <summary>True = advance paid to driver (debit / increases outstanding); false = recovery / settlement (credit).</summary>
        public bool IsAdvance { get; set; } = true;

        public decimal Amount { get; set; } = 0;

        public string? Reason { get; set; } // Trip advance, Fuel, Salary recovery, etc.

        public string? Remarks { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
