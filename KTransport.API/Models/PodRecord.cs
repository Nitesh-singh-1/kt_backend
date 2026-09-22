using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class PodRecord : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public long ShipmentId { get; set; }

        public virtual Shipment? Shipment { get; set; }

        public string? ShipmentNo { get; set; }

        public DateOnly DeliveryDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        public string ReceiverName { get; set; } = null!;

        public string? ReceiverMobile { get; set; }

        public string? ReceiverAadharOrId { get; set; }

        public string? DocumentUrl { get; set; } // Scanned Physical POD / Image

        public string? SignatureUrl { get; set; } // Digital E-Signature

        public PodStatus Status { get; set; } = PodStatus.Uploaded;

        public string? RejectionReason { get; set; }

        public string? Remarks { get; set; }

        public int? VerifiedByUserId { get; set; }

        public DateTime? VerifiedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
