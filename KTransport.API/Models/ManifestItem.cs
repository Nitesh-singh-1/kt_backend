using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class ManifestItem : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public long ManifestId { get; set; }

        public virtual Manifest Manifest { get; set; } = null!;

        public long ShipmentId { get; set; }

        public virtual Shipment Shipment { get; set; } = null!;

        public long? TargetDestinationHubId { get; set; }

        public virtual Location? TargetDestinationHub { get; set; }

        public int LoadedPackages { get; set; } = 0;

        public decimal LoadedWeightKg { get; set; } = 0;

        public ManifestItemStatus UnloadingStatus { get; set; } = ManifestItemStatus.Loaded;

        public int? ReceivedPackages { get; set; }

        public int? ShortagePackages { get; set; }

        public int? DamagedPackages { get; set; }

        public long? UnloadedAtHubId { get; set; }

        public virtual Location? UnloadedAtHub { get; set; }

        public DateTime? UnloadedDate { get; set; }

        public string? DiscrepancyRemarks { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
