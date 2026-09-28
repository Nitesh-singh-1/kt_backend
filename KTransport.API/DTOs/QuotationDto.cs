using System;
using System.ComponentModel.DataAnnotations;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    public class QuotationDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string QuoteNo { get; set; } = null!;
        public DateOnly QuoteDate { get; set; }
        public DateOnly? ValidUntil { get; set; }
        public long? PartyId { get; set; }
        public string? PartyName { get; set; }
        public string? PartyMobile { get; set; }
        public string? PartyGstNo { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public string? VehicleType { get; set; }
        public string? GoodsDescription { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? RatePerUnit { get; set; }
        public string? RateBasis { get; set; }
        public decimal EstimatedFreight { get; set; }
        public QuotationStatus Status { get; set; }
        public string StatusName => Status.ToString();
        public string? ConvertedRef { get; set; }
        public string? Terms { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateQuotationRequest
    {
        public DateOnly? QuoteDate { get; set; }
        public DateOnly? ValidUntil { get; set; }
        public long? PartyId { get; set; }

        [Required(ErrorMessage = "Customer / party name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Party name must be between 2 and 150 characters.")]
        public string PartyName { get; set; } = null!;

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Party mobile must be a valid 10-digit Indian mobile number.")]
        public string? PartyMobile { get; set; }

        [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Invalid GSTIN format.")]
        public string? PartyGstNo { get; set; }

        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public string? VehicleType { get; set; }
        public string? GoodsDescription { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? RatePerUnit { get; set; }
        public string? RateBasis { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Estimated freight cannot be negative.")]
        public decimal EstimatedFreight { get; set; } = 0;

        public string? Terms { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateQuotationRequest
    {
        public DateOnly? QuoteDate { get; set; }
        public DateOnly? ValidUntil { get; set; }
        public long? PartyId { get; set; }
        public string? PartyName { get; set; }

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Party mobile must be a valid 10-digit Indian mobile number.")]
        public string? PartyMobile { get; set; }

        [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Invalid GSTIN format.")]
        public string? PartyGstNo { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public string? VehicleType { get; set; }
        public string? GoodsDescription { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? RatePerUnit { get; set; }
        public string? RateBasis { get; set; }
        public decimal? EstimatedFreight { get; set; }
        public string? Terms { get; set; }
        public string? Notes { get; set; }
        public bool? IsActive { get; set; }
    }

    public class UpdateQuotationStatusRequest
    {
        [Required]
        public QuotationStatus Status { get; set; }
        /// <summary>Optional booking/GR reference to record when converting.</summary>
        public string? ConvertedRef { get; set; }
    }
}
