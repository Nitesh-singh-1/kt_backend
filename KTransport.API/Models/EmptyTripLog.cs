using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>A logged empty / deadhead vehicle run (no revenue cargo) — repositioning, return leg, etc.</summary>
    public class EmptyTripLog : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public long? VehicleId { get; set; }

        public virtual Vehicle? Vehicle { get; set; }

        public string? VehicleNo { get; set; }

        public string? DriverName { get; set; }

        public string? FromLocation { get; set; }

        public string? ToLocation { get; set; }

        public DateOnly TripDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        public decimal DistanceKm { get; set; } = 0;

        public decimal FuelCost { get; set; } = 0;

        public string? Reason { get; set; } // Return Run, Repositioning, Breakdown, Maintenance, Other

        public string? Remarks { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
