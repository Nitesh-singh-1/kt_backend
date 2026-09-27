using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>A vehicle finance / EMI loan against a fleet vehicle.</summary>
    public class VehicleLoan : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public long? VehicleId { get; set; }

        public virtual Vehicle? Vehicle { get; set; }

        public string? VehicleNo { get; set; }

        public string Lender { get; set; } = null!; // Bank / financier

        public string? LoanAccountNo { get; set; }

        public decimal PrincipalAmount { get; set; } = 0;

        public decimal EmiAmount { get; set; } = 0;

        public int TenureMonths { get; set; } = 0;

        public int EmisPaid { get; set; } = 0;

        public decimal InterestRate { get; set; } = 0;

        public DateOnly? LoanStartDate { get; set; }

        public DateOnly? NextDueDate { get; set; }

        public bool IsClosed { get; set; } = false;

        public string? Remarks { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
