using System;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    public class PodRecordDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public long ShipmentId { get; set; }
        public string? ShipmentNo { get; set; }
        public DateOnly DeliveryDate { get; set; }
        public string ReceiverName { get; set; } = null!;
        public string? ReceiverMobile { get; set; }
        public string? ReceiverAadharOrId { get; set; }
        public string? DocumentUrl { get; set; }
        public string? SignatureUrl { get; set; }
        public PodStatus Status { get; set; }
        public string StatusName => Status.ToString();
        public string? RejectionReason { get; set; }
        public string? Remarks { get; set; }
        public int? VerifiedByUserId { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UploadPodRequest
    {
        public long ShipmentId { get; set; }
        public DateOnly? DeliveryDate { get; set; }
        public string ReceiverName { get; set; } = null!;
        public string? ReceiverMobile { get; set; }
        public string? ReceiverAadharOrId { get; set; }
        public string? DocumentUrl { get; set; }
        public string? SignatureUrl { get; set; }
        public string? Remarks { get; set; }
    }

    public class VerifyPodRequest
    {
        public bool IsApproved { get; set; } = true;
        public string? RejectionReason { get; set; }
        public string? Remarks { get; set; }
    }

    /// <summary>Lightweight shipment shape for the POD-upload dropdown (consignments still awaiting a POD).</summary>
    public class PodPendingShipmentDto
    {
        public long Id { get; set; }
        public string? ShipmentNo { get; set; }
        public string? ConsignorName { get; set; }
        public string? ConsigneeName { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public ShipmentStatus Status { get; set; }
        public string StatusName => Status.ToString();
    }
}
