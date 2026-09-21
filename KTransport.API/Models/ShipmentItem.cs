using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class ShipmentItem : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public long ShipmentId { get; set; }

        public virtual Shipment Shipment { get; set; } = null!;

        public string? Article { get; set; }

        public string? Description { get; set; }

        public decimal Weight { get; set; } = 0;

        public decimal Rate { get; set; } = 0;

        public int Quantity { get; set; } = 1;

        public decimal TotalAmount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
