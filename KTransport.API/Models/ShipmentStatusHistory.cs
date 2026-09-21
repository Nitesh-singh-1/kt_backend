using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class ShipmentStatusHistory : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public long ShipmentId { get; set; }

        public virtual Shipment Shipment { get; set; } = null!;

        public ShipmentStatus FromStatus { get; set; }

        public ShipmentStatus ToStatus { get; set; }

        public string? Location { get; set; }

        public string? Remarks { get; set; }

        public int? ChangedByUserId { get; set; }

        public virtual User? ChangedByUser { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    }
}
