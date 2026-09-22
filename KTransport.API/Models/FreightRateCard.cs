using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class FreightRateCard : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        // Optional specific party (null means standard default tariff for all customers)
        public long? PartyId { get; set; }

        public virtual Party? Party { get; set; }

        public string? PartyName { get; set; }

        public string FromLocation { get; set; } = null!;

        public string ToLocation { get; set; } = null!;

        public string? CommodityType { get; set; }

        public RateType RateType { get; set; } = RateType.PerKg;

        public decimal BaseRate { get; set; } = 0;

        public decimal MinFreightAmount { get; set; } = 0;

        public decimal HamaliRatePerKg { get; set; } = 0;

        public decimal DoorDeliveryCharge { get; set; } = 0;

        public decimal StationaryCharge { get; set; } = 0;

        public DateOnly? EffectiveFrom { get; set; }

        public DateOnly? EffectiveTo { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
