using System;
using System.Collections.Generic;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class Shipment : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string ShipmentNo { get; set; } = null!; // Waybill / GR / LR Number

        public string? InvoiceNo { get; set; }

        public long? InvoiceId { get; set; }

        public virtual Invoice? Invoice { get; set; }

        public DateOnly ShipmentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        public DateOnly? InvoiceDate { get; set; }

        public string? FromLocation { get; set; }

        public string? ToLocation { get; set; }

        public string? TruckNo { get; set; }

        public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.NonTaxable;

        public string? GstPaidBy { get; set; }

        // Consignor (Sender)
        public long? ConsignorPartyId { get; set; }

        public virtual Party? ConsignorParty { get; set; }

        public string? ConsignorName { get; set; }

        public string? ConsignorGstNo { get; set; }

        public string? ConsignorMobile { get; set; }

        public string? ConsignorAddress { get; set; }

        // Consignee (Receiver)
        public long? ConsigneePartyId { get; set; }

        public virtual Party? ConsigneeParty { get; set; }

        public string? ConsigneeName { get; set; }

        public string? ConsigneeGstNo { get; set; }

        public string? ConsigneeMobile { get; set; }

        public string? ConsigneeAddress { get; set; }

        // Financials
        public decimal GoodsValue { get; set; } = 0;

        public PaymentTerm PaymentTerm { get; set; } = PaymentTerm.ToPay;

        public decimal TotalFreight { get; set; } = 0;

        public decimal TotalOtherCharges { get; set; } = 0;

        public decimal TotalTaxAmount { get; set; } = 0;

        public decimal GrandTotal { get; set; } = 0;

        public decimal PaidAmount { get; set; } = 0;

        public decimal DueAmount { get; set; } = 0;

        // Lifecycle Status
        public ShipmentStatus Status { get; set; } = ShipmentStatus.Booked;

        public string? Remarks { get; set; }

        public string? BookingClerk { get; set; }

        public bool IsActive { get; set; } = true;

        public int? CreatedBy { get; set; }

        public int? UpdatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Hubs & Routing
        public long? OriginHubId { get; set; }

        public virtual Location? OriginHub { get; set; }

        public long? DestinationHubId { get; set; }

        public virtual Location? DestinationHub { get; set; }

        public long? CurrentHubId { get; set; }

        public virtual Location? CurrentHub { get; set; }

        public string? DeliveryType { get; set; } // DoorDelivery, GodownDelivery

        public string? EwayBillNo { get; set; }

        public DateTime? EwayBillValidUpto { get; set; }

        // Navigation properties
        public virtual User? CreatedByNavigation { get; set; }

        public virtual User? UpdatedByNavigation { get; set; }

        public virtual ICollection<ShipmentItem> Items { get; set; } = new List<ShipmentItem>();

        public virtual ICollection<ShipmentChargeItem> ChargeItems { get; set; } = new List<ShipmentChargeItem>();

        public virtual ICollection<ShipmentStatusHistory> StatusHistory { get; set; } = new List<ShipmentStatusHistory>();

        public virtual ICollection<ConsignmentInvoiceReference> InvoiceReferences { get; set; } = new List<ConsignmentInvoiceReference>();

        public virtual ICollection<ManifestItem> ManifestItems { get; set; } = new List<ManifestItem>();
    }
}
