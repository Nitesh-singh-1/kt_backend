using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class Tyre : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string SerialNo { get; set; } = null!; // Tyre serial / batch number

        public string? Brand { get; set; }

        public string? Size { get; set; } // e.g. 10.00 R20

        // Fitment (null when in stock)
        public long? VehicleId { get; set; }

        public virtual Vehicle? Vehicle { get; set; }

        public string? VehicleNo { get; set; }

        public string? Position { get; set; } // Front-Left, Rear-Right-Outer, Stepney, etc.

        public DateOnly? PurchaseDate { get; set; }

        public decimal PurchaseCost { get; set; } = 0;

        public decimal PurchaseOdometer { get; set; } = 0;

        public decimal CurrentOdometer { get; set; } = 0;

        public int RetreadCount { get; set; } = 0;

        public TyreStatus Status { get; set; } = TyreStatus.InStock;

        public DateOnly? DisposalDate { get; set; }

        public string? Remarks { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
