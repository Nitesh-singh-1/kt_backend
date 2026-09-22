using System;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    public class VehicleMaintenanceDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public long VehicleId { get; set; }
        public string? VehicleNo { get; set; }
        public MaintenanceType MaintenanceType { get; set; }
        public string MaintenanceTypeName => MaintenanceType.ToString();
        public decimal Cost { get; set; }
        public DateOnly ServiceDate { get; set; }
        public decimal OdometerReading { get; set; }
        public decimal? NextServiceDueKm { get; set; }
        public DateOnly? NextServiceDueDate { get; set; }
        public string? WorkshopName { get; set; }
        public string? InvoiceNo { get; set; }
        public string? Description { get; set; }
        public string? Remarks { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateMaintenanceRequest
    {
        public long VehicleId { get; set; }
        public MaintenanceType MaintenanceType { get; set; } = MaintenanceType.RoutineService;
        public decimal Cost { get; set; }
        public DateOnly? ServiceDate { get; set; }
        public decimal OdometerReading { get; set; } = 0;
        public decimal? NextServiceDueKm { get; set; }
        public DateOnly? NextServiceDueDate { get; set; }
        public string? WorkshopName { get; set; }
        public string? InvoiceNo { get; set; }
        public string? Description { get; set; }
        public string? Remarks { get; set; }
    }
}
