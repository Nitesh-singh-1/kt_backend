using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class LorryHireContract : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string ContractNo { get; set; } = null!; // Lorry Hire Slip Number e.g. LHS-2026-0001

        public DateOnly ContractDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        public long? TripId { get; set; }

        public virtual Trip? Trip { get; set; }

        public long? VendorId { get; set; }

        public virtual Vendor? Vendor { get; set; }

        public string? VendorName { get; set; }

        public string? VehicleNo { get; set; }

        public string? DriverName { get; set; }

        public string? DriverMobile { get; set; }

        public string? FromLocation { get; set; }

        public string? ToLocation { get; set; }

        // Financials
        public decimal TotalHireAmount { get; set; } = 0; // Agreed Freight Payable to Vendor

        public decimal AdvanceCashPaid { get; set; } = 0;

        public decimal DieselAdvanceAmount { get; set; } = 0;

        public decimal TdsAmount { get; set; } = 0;

        public decimal OtherDeductions { get; set; } = 0;

        public decimal BalancePayable { get; set; } = 0;

        public decimal PaidBalanceAmount { get; set; } = 0;

        public string? PaymentStatus { get; set; } = "Unpaid"; // Unpaid, PartiallyPaid, Paid

        public string? PaymentReference { get; set; }

        public string? Remarks { get; set; }

        public bool IsActive { get; set; } = true;

        public int? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
