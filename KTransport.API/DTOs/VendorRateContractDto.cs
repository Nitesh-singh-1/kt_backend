using System;
using System.ComponentModel.DataAnnotations;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    public class VendorRateContractDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public long? VendorId { get; set; }
        public string? VendorName { get; set; }
        public string FromLocation { get; set; } = null!;
        public string ToLocation { get; set; } = null!;
        public string? VehicleType { get; set; }
        public RateType RateType { get; set; }
        public string RateTypeName => RateType.ToString();
        public decimal HireRate { get; set; }
        public decimal MinGuaranteeAmount { get; set; }
        public decimal LoadingCharge { get; set; }
        public decimal UnloadingCharge { get; set; }
        public DateOnly? EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }
        public string? Remarks { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateVendorRateContractRequest
    {
        public long? VendorId { get; set; }

        [Required(ErrorMessage = "Vendor / transporter name is required.")]
        public string VendorName { get; set; } = null!;

        [Required(ErrorMessage = "Origin is required.")]
        public string FromLocation { get; set; } = null!;

        [Required(ErrorMessage = "Destination is required.")]
        public string ToLocation { get; set; } = null!;

        public string? VehicleType { get; set; }
        public RateType RateType { get; set; } = RateType.PerTrip;
        public decimal HireRate { get; set; }
        public decimal MinGuaranteeAmount { get; set; } = 0;
        public decimal LoadingCharge { get; set; } = 0;
        public decimal UnloadingCharge { get; set; } = 0;
        public DateOnly? EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }
        public string? Remarks { get; set; }
    }

    public class UpdateVendorRateContractRequest
    {
        public long? VendorId { get; set; }
        public string? VendorName { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public string? VehicleType { get; set; }
        public RateType? RateType { get; set; }
        public decimal? HireRate { get; set; }
        public decimal? MinGuaranteeAmount { get; set; }
        public decimal? LoadingCharge { get; set; }
        public decimal? UnloadingCharge { get; set; }
        public DateOnly? EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }
        public string? Remarks { get; set; }
        public bool? IsActive { get; set; }
    }
}
