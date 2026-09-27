using System;
using System.ComponentModel.DataAnnotations;

namespace KTransport.API.DTOs
{
    public class DriverLedgerEntryDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public long? DriverId { get; set; }
        public string DriverName { get; set; } = null!;
        public DateOnly EntryDate { get; set; }
        public bool IsAdvance { get; set; }
        public decimal Amount { get; set; }
        public string? Reason { get; set; }
        public string? Remarks { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateDriverLedgerEntryRequest
    {
        public long? DriverId { get; set; }

        [Required(ErrorMessage = "Driver name is required.")]
        public string DriverName { get; set; } = null!;

        public DateOnly? EntryDate { get; set; }
        public bool IsAdvance { get; set; } = true;

        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount { get; set; }

        public string? Reason { get; set; }
        public string? Remarks { get; set; }
    }

    public class UpdateDriverLedgerEntryRequest
    {
        public long? DriverId { get; set; }
        public string? DriverName { get; set; }
        public DateOnly? EntryDate { get; set; }
        public bool? IsAdvance { get; set; }
        public decimal? Amount { get; set; }
        public string? Reason { get; set; }
        public string? Remarks { get; set; }
        public bool? IsActive { get; set; }
    }

    /// <summary>Per-driver rolled-up outstanding (advances minus recoveries).</summary>
    public class DriverOutstandingDto
    {
        public string DriverName { get; set; } = null!;
        public decimal TotalAdvance { get; set; }
        public decimal TotalRecovered { get; set; }
        public decimal Outstanding { get; set; }
        public int EntryCount { get; set; }
    }
}
