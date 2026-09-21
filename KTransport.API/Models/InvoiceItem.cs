using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class InvoiceItem : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public long InvoiceId { get; set; }

        public virtual Invoice? Invoice { get; set; }

        public long? ShipmentId { get; set; }

        public virtual Shipment? Shipment { get; set; }

        public string? ShipmentNo { get; set; }

        public string Description { get; set; } = null!;

        public decimal Quantity { get; set; } = 1;

        public decimal Rate { get; set; } = 0;

        public decimal Amount { get; set; } = 0;

        public decimal TaxRate { get; set; } = 0;

        public decimal TaxAmount { get; set; } = 0;

        public decimal TotalAmount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
