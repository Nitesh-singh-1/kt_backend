using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>A workshop / garage spare-part stock item (inventory master).</summary>
    public class SparePart : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string PartName { get; set; } = null!;

        public string? PartNo { get; set; }

        public string? Category { get; set; } // Engine, Electrical, Tyre, Body, Consumable, etc.

        public string? Unit { get; set; } // pcs, litre, set

        public decimal StockQuantity { get; set; } = 0;

        public decimal ReorderLevel { get; set; } = 0;

        public decimal UnitCost { get; set; } = 0;

        public string? StoreLocation { get; set; }

        public string? Supplier { get; set; }

        public string? Remarks { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
