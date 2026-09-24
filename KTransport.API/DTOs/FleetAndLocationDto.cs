using System;
using System.ComponentModel.DataAnnotations;

namespace KTransport.API.DTOs
{
    // Vehicle DTOs
    public class VehicleDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string VehicleNo { get; set; } = null!;
        public string? VehicleType { get; set; }
        public string? OwnerType { get; set; }
        public decimal CapacityTons { get; set; }
        public string? EngineNo { get; set; }
        public string? ChassisNo { get; set; }
        public DateOnly? FitnessValidUntil { get; set; }
        public DateOnly? InsuranceValidUntil { get; set; }
        public DateOnly? PermitValidUntil { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateVehicleRequest
    {
        [Required(ErrorMessage = "Vehicle / Truck registration number is required.")]
        [RegularExpression(@"^[a-zA-Z0-9\-\s]+$", ErrorMessage = "Vehicle registration number contains invalid characters.")]
        public string VehicleNo { get; set; } = null!;

        public string? VehicleType { get; set; }
        public string? OwnerType { get; set; }
        public decimal CapacityTons { get; set; } = 0;
        public string? EngineNo { get; set; }
        public string? ChassisNo { get; set; }
        public DateOnly? FitnessValidUntil { get; set; }
        public DateOnly? InsuranceValidUntil { get; set; }
        public DateOnly? PermitValidUntil { get; set; }
    }

    public class VehicleLookupDto
    {
        public long Id { get; set; }
        public string VehicleNo { get; set; } = null!;
        public string? VehicleType { get; set; }
        public string? OwnerType { get; set; }
        public decimal CapacityTons { get; set; }
    }

    // Driver DTOs
    public class DriverDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = null!;
        public string? Mobile { get; set; }
        public string? LicenseNo { get; set; }
        public DateOnly? LicenseValidUntil { get; set; }
        public string? AadharNo { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateDriverRequest
    {
        [Required(ErrorMessage = "Driver Full Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Driver name must be between 2 and 100 characters.")]
        [RegularExpression(@"^[a-zA-Z\s\.\-]+$", ErrorMessage = "Driver name contains invalid characters. Only letters, spaces, dots, and hyphens are allowed.")]
        public string Name { get; set; } = null!;

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Mobile number must be a valid 10-digit Indian mobile number.")]
        public string? Mobile { get; set; }

        public string? LicenseNo { get; set; }
        public DateOnly? LicenseValidUntil { get; set; }
        public string? AadharNo { get; set; }
        public string? Address { get; set; }
    }

    public class DriverLookupDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Mobile { get; set; }
        public string? LicenseNo { get; set; }
    }

    // Location / Station DTOs
    public class LocationDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Address { get; set; }
        public string? Pincode { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateLocationRequest
    {
        public string? Code { get; set; }

        [Required(ErrorMessage = "Station / Hub Name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Station name must be between 2 and 150 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\.\,\&\-\'\(\)\/\#]+$", ErrorMessage = "Station / Hub name contains invalid characters.")]
        public string Name { get; set; } = null!;

        public string? City { get; set; }
        public string? State { get; set; }
        public string? Address { get; set; }

        [RegularExpression(@"^\d{6}$", ErrorMessage = "Pincode must be exactly 6 digits.")]
        public string? Pincode { get; set; }
    }

    public class LocationLookupDto
    {
        public long Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
    }
}
