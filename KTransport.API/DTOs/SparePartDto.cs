using System;
using System.ComponentModel.DataAnnotations;

namespace KTransport.API.DTOs
{
    public class SparePartDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string PartName { get; set; } = null!;
        public string? PartNo { get; set; }
        public string? Category { get; set; }
        public string? Unit { get; set; }
        public decimal StockQuantity { get; set; }
        public decimal ReorderLevel { get; set; }
        public decimal UnitCost { get; set; }
        public decimal StockValue => StockQuantity * UnitCost;
        public bool IsLowStock => ReorderLevel > 0 && StockQuantity <= ReorderLevel;
        public string? StoreLocation { get; set; }
        public string? Supplier { get; set; }
        public string? Remarks { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateSparePartRequest
    {
        [Required(ErrorMessage = "Part name is required.")]
        public string PartName { get; set; } = null!;
        public string? PartNo { get; set; }
        public string? Category { get; set; }
        public string? Unit { get; set; }
        public decimal StockQuantity { get; set; } = 0;
        public decimal ReorderLevel { get; set; } = 0;
        public decimal UnitCost { get; set; } = 0;
        public string? StoreLocation { get; set; }
        public string? Supplier { get; set; }
        public string? Remarks { get; set; }
    }

    public class UpdateSparePartRequest
    {
        public string? PartName { get; set; }
        public string? PartNo { get; set; }
        public string? Category { get; set; }
        public string? Unit { get; set; }
        public decimal? StockQuantity { get; set; }
        public decimal? ReorderLevel { get; set; }
        public decimal? UnitCost { get; set; }
        public string? StoreLocation { get; set; }
        public string? Supplier { get; set; }
        public string? Remarks { get; set; }
        public bool? IsActive { get; set; }
    }

    /// <summary>Stock movement (issue to a vehicle / receive into store) that adjusts on-hand quantity.</summary>
    public class SparePartStockMovementRequest
    {
        /// <summary>Positive to receive into stock, negative to issue out.</summary>
        [Required]
        public decimal QuantityDelta { get; set; }
        public string? Reason { get; set; } // e.g. "Issued to MH12AB1234", "Purchase GRN #123"
    }
}
