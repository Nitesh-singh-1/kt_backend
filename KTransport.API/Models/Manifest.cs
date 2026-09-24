using System;
using System.Collections.Generic;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class Manifest : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string ManifestNo { get; set; } = null!; // Loading Sheet Number

        public DateOnly ManifestDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        public long? OriginHubId { get; set; }

        public virtual Location? OriginHub { get; set; }

        public long? DestinationHubId { get; set; }

        public virtual Location? DestinationHub { get; set; }

        public long? TripId { get; set; }

        public virtual Trip? Trip { get; set; }

        public string? ConsolidatedEwayBillNo { get; set; } // Form GST EWB-02

        public DateTime? ConsolidatedEwayBillDate { get; set; }

        public string? SealNo { get; set; }

        public string? LoadingSupervisorName { get; set; }

        public string? Remarks { get; set; }

        public ManifestStatus Status { get; set; } = ManifestStatus.Draft;

        public int TotalConsignments { get; set; } = 0;

        public int TotalPackages { get; set; } = 0;

        public decimal TotalWeightKg { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public int? CreatedBy { get; set; }

        public int? UpdatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual User? CreatedByNavigation { get; set; }

        public virtual User? UpdatedByNavigation { get; set; }

        public virtual ICollection<ManifestItem> Items { get; set; } = new List<ManifestItem>();
    }
}
