using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>Negotiated lorry-hire rate we PAY a market vendor/transporter for a route (vs. FreightRateCard which is what we CHARGE customers).</summary>
    public class VendorRateContract : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public long? VendorId { get; set; }

        public virtual Vendor? Vendor { get; set; }

        public string? VendorName { get; set; }

        public string FromLocation { get; set; } = null!;

        public string ToLocation { get; set; } = null!;

        public string? VehicleType { get; set; }

        public RateType RateType { get; set; } = RateType.PerTrip;

        public decimal HireRate { get; set; } = 0;

        public decimal MinGuaranteeAmount { get; set; } = 0;

        public decimal LoadingCharge { get; set; } = 0;

        public decimal UnloadingCharge { get; set; } = 0;

        public DateOnly? EffectiveFrom { get; set; }

        public DateOnly? EffectiveTo { get; set; }

        public string? Remarks { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
