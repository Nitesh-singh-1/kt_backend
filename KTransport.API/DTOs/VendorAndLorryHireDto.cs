using System;

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
        public decimal TdsPercentage { get; set; } = 1.0m;
        public string? BankName { get; set; }
        public string? AccountNumber { get; set; }
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
        public string? DriverMobile { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public decimal TotalHireAmount { get; set; }
        public decimal AdvanceCashPaid { get; set; } = 0;
        public decimal DieselAdvanceAmount { get; set; } = 0;
        public decimal TdsAmount { get; set; } = 0;
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
