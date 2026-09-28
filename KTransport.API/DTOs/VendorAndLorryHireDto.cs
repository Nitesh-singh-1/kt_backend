using System;
using System.ComponentModel.DataAnnotations;

namespace KTransport.API.DTOs
{
    public class VendorDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = null!;
        public string? Code { get; set; }
        public string? PanNo { get; set; }
        public string? GstNo { get; set; }
        public string? ContactPerson { get; set; }
        public string? Mobile { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public decimal TdsPercentage { get; set; }
        public string? BankName { get; set; }
        public string? AccountNumber { get; set; }
        public string? IfscCode { get; set; }
        public string? AccountHolderName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateVendorRequest
    {
        [Required(ErrorMessage = "Vendor / Transporter name is required.")]
        [StringLength(200, MinimumLength = 2)]
        public string Name { get; set; } = null!;

        [StringLength(50)]
        public string? Code { get; set; }

        [RegularExpression(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$", ErrorMessage = "Invalid PAN format. Expected 10 alphanumeric characters (e.g. AAAAA0000A).")]
        public string? PanNo { get; set; }

        [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Invalid GSTIN format. Expected 15 characters (e.g. 27AAAAA0000A1Z5).")]
        public string? GstNo { get; set; }

        [StringLength(100)]
        public string? ContactPerson { get; set; }

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Mobile number must be a valid 10-digit Indian mobile number.")]
        public string? Mobile { get; set; }

        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        public string? Email { get; set; }

        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }

        [Range(0, 30, ErrorMessage = "TDS percentage must be between 0 and 30.")]
        public decimal TdsPercentage { get; set; } = 1.0m;

        public string? BankName { get; set; }
        public string? AccountNumber { get; set; }

        [RegularExpression(@"^[A-Z]{4}0[A-Z0-9]{6}$", ErrorMessage = "Invalid IFSC code. Expected 11 characters (e.g. SBIN0001234).")]
        public string? IfscCode { get; set; }

        public string? AccountHolderName { get; set; }
    }

    public class VendorLookupDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Code { get; set; }
        public string? PanNo { get; set; }
        public string? Mobile { get; set; }
        public decimal TdsPercentage { get; set; }
    }

    public class LorryHireContractDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string ContractNo { get; set; } = null!;
        public DateOnly ContractDate { get; set; }
        public long? TripId { get; set; }
        public long? VendorId { get; set; }
        public string? VendorName { get; set; }
        public string? VehicleNo { get; set; }
        public string? DriverName { get; set; }
        public string? DriverMobile { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public decimal TotalHireAmount { get; set; }
        public decimal AdvanceCashPaid { get; set; }
        public decimal DieselAdvanceAmount { get; set; }
        public decimal TdsAmount { get; set; }
        public decimal OtherDeductions { get; set; }
        public decimal BalancePayable { get; set; }
        public decimal PaidBalanceAmount { get; set; }
        public string? PaymentStatus { get; set; }
        public string? PaymentReference { get; set; }
        public string? Remarks { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateLorryHireRequest
    {
        public string? ContractNo { get; set; }
        public DateOnly? ContractDate { get; set; }
        public long? TripId { get; set; }
        public long? VendorId { get; set; }
        public string? VendorName { get; set; }
        public string? VehicleNo { get; set; }
        public string? DriverName { get; set; }

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Driver mobile must be a valid 10-digit Indian mobile number.")]
        public string? DriverMobile { get; set; }

        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Hire amount cannot be negative.")]
        public decimal TotalHireAmount { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Advance cannot be negative.")]
        public decimal AdvanceCashPaid { get; set; } = 0;

        [Range(0, double.MaxValue, ErrorMessage = "Diesel advance cannot be negative.")]
        public decimal DieselAdvanceAmount { get; set; } = 0;

        [Range(0, double.MaxValue, ErrorMessage = "TDS cannot be negative.")]
        public decimal TdsAmount { get; set; } = 0;

        [Range(0, double.MaxValue, ErrorMessage = "Deductions cannot be negative.")]
        public decimal OtherDeductions { get; set; } = 0;

        public string? Remarks { get; set; }
    }

    public class RecordLorryHirePaymentRequest
    {
        public decimal Amount { get; set; }
        public string? PaymentReference { get; set; }
        public string? Remarks { get; set; }
    }
}
