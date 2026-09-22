using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class TripShipment : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public long TripId { get; set; }

        public virtual Trip? Trip { get; set; }

        public long ShipmentId { get; set; }

        public virtual Shipment? Shipment { get; set; }

        public string? ShipmentNo { get; set; }

        public decimal LoadedWeight { get; set; } = 0;

        public int LoadedPackages { get; set; } = 0;

        public decimal FreightAmount { get; set; } = 0;

        public DateTime LoadedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UnloadedAt { get; set; }

        public string? Remarks { get; set; }
    }
}
