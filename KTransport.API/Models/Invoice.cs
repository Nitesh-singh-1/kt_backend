using System;
using System.Collections.Generic;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class Invoice : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string InvoiceNo { get; set; } = null!;

        public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        public DateOnly? DueDate { get; set; }

        public long? PartyId { get; set; }

        public virtual Party? Party { get; set; }

        public string? PartyName { get; set; }

        public string? PartyGstNo { get; set; }

        public string? PartyAddress { get; set; }

        public decimal SubTotal { get; set; } = 0;

        public decimal TaxRate { get; set; } = 0;

        public decimal TaxAmount { get; set; } = 0;

        public decimal Discount { get; set; } = 0;

        public decimal OtherCharges { get; set; } = 0;

        public decimal GrandTotal { get; set; } = 0;

        public decimal PaidAmount { get; set; } = 0;

        public decimal DueAmount { get; set; } = 0;

        public InvoicePaymentStatus PaymentStatus { get; set; } = InvoicePaymentStatus.Unpaid;

        public string? PaymentMode { get; set; }

        public string? Remarks { get; set; }

        public bool IsActive { get; set; } = true;

        public int? CreatedBy { get; set; }

        public int? UpdatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();

        public virtual ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();
    }
}
