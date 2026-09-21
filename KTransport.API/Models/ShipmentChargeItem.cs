using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class ShipmentChargeItem : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public long ShipmentId { get; set; }

        public virtual Shipment Shipment { get; set; } = null!;

        public int? ChargeTypeId { get; set; }

        public virtual ChargeType? ChargeType { get; set; }

        public string ChargeName { get; set; } = null!;

        public decimal Amount { get; set; } = 0;

        public bool IsTaxable { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
