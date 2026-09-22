using System;
using System.Collections.Generic;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class Trip : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string TripNo { get; set; } = null!; // Manifest / Loading Sheet / Challan Number

        public DateOnly TripDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        // Vehicle & Fleet
        public long? VehicleId { get; set; }

        public virtual Vehicle? Vehicle { get; set; }

        public string? VehicleNo { get; set; }

        // Driver
        public long? DriverId { get; set; }

        public virtual Driver? Driver { get; set; }

        public string? DriverName { get; set; }

        public string? DriverMobile { get; set; }

        // Route
        public long? OriginLocationId { get; set; }

        public virtual Location? OriginLocation { get; set; }

        public string? OriginLocationName { get; set; }

        public long? DestinationLocationId { get; set; }

        public virtual Location? DestinationLocation { get; set; }

        public string? DestinationLocationName { get; set; }

        // Status & Lifecycle
        public TripStatus Status { get; set; } = TripStatus.Draft;

        public DateTime? DepartureTime { get; set; }

        public DateTime? ArrivalTime { get; set; }

        public decimal StartOdometer { get; set; } = 0;

        public decimal EndOdometer { get; set; } = 0;

        public string? SealNo { get; set; }

        public string? Remarks { get; set; }

        // Metrics & Financials
        public decimal TotalWeightTons { get; set; } = 0;

        public int TotalPackages { get; set; } = 0;

        public decimal TotalFreightRevenue { get; set; } = 0;

        public decimal DriverAdvanceCash { get; set; } = 0;

        public decimal DriverAdvanceFuel { get; set; } = 0;

        public decimal TotalExpenses { get; set; } = 0;

        public decimal NetProfitMargin => TotalFreightRevenue - (DriverAdvanceCash + DriverAdvanceFuel + TotalExpenses);

        public bool IsActive { get; set; } = true;

        public int? CreatedBy { get; set; }

        public int? UpdatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Child Collections
        public virtual ICollection<TripShipment> Shipments { get; set; } = new List<TripShipment>();

        public virtual ICollection<TripExpense> Expenses { get; set; } = new List<TripExpense>();
    }
}
