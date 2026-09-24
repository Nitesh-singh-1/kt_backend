using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class ConsignmentInvoiceReference : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public long ShipmentId { get; set; }

        public virtual Shipment Shipment { get; set; } = null!;

        public string CustomerInvoiceNo { get; set; } = null!;

        public DateOnly CustomerInvoiceDate { get; set; }

        public decimal DeclaredGoodsValue { get; set; } = 0;

        public string? EwayBillNo { get; set; }

        public DateOnly? EwayBillDate { get; set; }

        public DateTime? EwayBillValidUpto { get; set; }

        public string? DocumentType { get; set; } // TaxInvoice, DeliveryChallan, BillOfSupply, JobWorkChallan

        public int? PackageCount { get; set; }

        public decimal? WeightKg { get; set; }

        public string? CommodityDescription { get; set; }

        public string? DocumentUrl { get; set; } // Scanned customer paper bill / photo

        public bool IsActive { get; set; } = true;

        public int? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual User? CreatedByNavigation { get; set; }
    }
}
