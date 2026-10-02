using System;
using System.Collections.Generic;
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
        public DateOnly? PucValidUntil { get; set; }
        public DateOnly? TaxValidUntil { get; set; }
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
        public DateOnly? PucValidUntil { get; set; }
        public DateOnly? TaxValidUntil { get; set; }
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
        public string? ContactNumber { get; set; }
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

        [RegularExpression(@"^[0-9+\-\s()]{0,20}$", ErrorMessage = "Contact number may contain digits, spaces and + - ( ) only (max 20 chars).")]
        public string? ContactNumber { get; set; }
    }

    public class UpdateVehicleRequest
    {
        public string? VehicleNo { get; set; }
        public string? VehicleType { get; set; }
        public string? OwnerType { get; set; }
        public decimal? CapacityTons { get; set; }
        public string? EngineNo { get; set; }
        public string? ChassisNo { get; set; }
        public DateOnly? FitnessValidUntil { get; set; }
        public DateOnly? InsuranceValidUntil { get; set; }
        public DateOnly? PermitValidUntil { get; set; }
        public DateOnly? PucValidUntil { get; set; }
        public DateOnly? TaxValidUntil { get; set; }
        public bool? IsActive { get; set; }
    }

    public class UpdateDriverRequest
    {
        public string? Name { get; set; }
        public string? Mobile { get; set; }
        public string? LicenseNo { get; set; }
        public DateOnly? LicenseValidUntil { get; set; }
        public string? AadharNo { get; set; }
        public string? Address { get; set; }
        public bool? IsActive { get; set; }
    }

    public class UpdateLocationRequest
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Address { get; set; }
        public string? Pincode { get; set; }

        [RegularExpression(@"^[0-9+\-\s()]{0,20}$", ErrorMessage = "Contact number may contain digits, spaces and + - ( ) only (max 20 chars).")]
        public string? ContactNumber { get; set; }

        public bool? IsActive { get; set; }
    }

    public class LocationLookupDto
    {
        public long Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        public string? ContactNumber { get; set; }
    }

    // --- Fleet Compliance (document expiry tracking) ---

    /// <summary>A single expiring / expired statutory document for a vehicle or driver.</summary>
    public class ComplianceAlertDto
    {
        /// <summary>"Vehicle" or "Driver".</summary>
        public string EntityType { get; set; } = null!;
        public long EntityId { get; set; }
        /// <summary>Registration number (vehicle) or driver name.</summary>
        public string EntityName { get; set; } = null!;
        /// <summary>Human label, e.g. "Insurance", "Fitness Certificate", "Driving License".</summary>
        public string DocumentType { get; set; } = null!;
        public DateOnly ExpiryDate { get; set; }
        /// <summary>Days until expiry; negative when already expired.</summary>
        public int DaysToExpiry { get; set; }
        /// <summary>"Expired", "Critical" (&lt;=7d), "Warning" (&lt;=30d) or "Upcoming".</summary>
        public string Status { get; set; } = null!;
    }

    public class ComplianceOverviewDto
    {
        public int ExpiredCount { get; set; }
        public int CriticalCount { get; set; }
        public int WarningCount { get; set; }
        public int UpcomingCount { get; set; }
        /// <summary>Total statutory documents inspected across active vehicles and drivers.</summary>
        public int TrackedDocuments { get; set; }
        public List<ComplianceAlertDto> Alerts { get; set; } = new();
    }
}
