using System;
using System.ComponentModel.DataAnnotations;

namespace KTransport.API.DTOs
{
    public class EmptyTripLogDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public long? VehicleId { get; set; }
        public string? VehicleNo { get; set; }
        public string? DriverName { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public DateOnly TripDate { get; set; }
        public decimal DistanceKm { get; set; }
        public decimal FuelCost { get; set; }
        public string? Reason { get; set; }
        public string? Remarks { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateEmptyTripLogRequest
    {
        public long? VehicleId { get; set; }

        [Required(ErrorMessage = "Vehicle number is required.")]
        public string VehicleNo { get; set; } = null!;

        public string? DriverName { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public DateOnly? TripDate { get; set; }
        public decimal DistanceKm { get; set; } = 0;
        public decimal FuelCost { get; set; } = 0;
        public string? Reason { get; set; }
        public string? Remarks { get; set; }
    }

    public class UpdateEmptyTripLogRequest
    {
        public long? VehicleId { get; set; }
        public string? VehicleNo { get; set; }
        public string? DriverName { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public DateOnly? TripDate { get; set; }
        public decimal? DistanceKm { get; set; }
        public decimal? FuelCost { get; set; }
        public string? Reason { get; set; }
        public string? Remarks { get; set; }
        public bool? IsActive { get; set; }
    }
}
