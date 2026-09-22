using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class VehicleMaintenance : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public long VehicleId { get; set; }

        public virtual Vehicle? Vehicle { get; set; }

        public string? VehicleNo { get; set; }

        public MaintenanceType MaintenanceType { get; set; } = MaintenanceType.RoutineService;

        public decimal Cost { get; set; } = 0;

        public DateOnly ServiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        public decimal OdometerReading { get; set; } = 0;

        public decimal? NextServiceDueKm { get; set; }

        public DateOnly? NextServiceDueDate { get; set; }

        public string? WorkshopName { get; set; }

        public string? InvoiceNo { get; set; }

        public string? Description { get; set; }

        public string? Remarks { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
