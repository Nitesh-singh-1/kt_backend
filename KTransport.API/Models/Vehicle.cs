using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class Vehicle : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string VehicleNo { get; set; } = null!; // Registration Number e.g. MH12AB1234

        public string? VehicleType { get; set; } // 10 Wheeler, Container, Trailer, etc.

        public string? OwnerType { get; set; } // Own, Attached, Market

        public decimal CapacityTons { get; set; } = 0;

        public string? EngineNo { get; set; }

        public string? ChassisNo { get; set; }

        public DateOnly? FitnessValidUntil { get; set; }

        public DateOnly? InsuranceValidUntil { get; set; }

        public DateOnly? PermitValidUntil { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
