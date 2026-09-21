using System;

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
        public string Name { get; set; } = null!;
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

    // Location DTOs
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
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Address { get; set; }
        public string? Pincode { get; set; }
    }

    public class LocationLookupDto
    {
        public long Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? City { get; set; }
        public string? State { get; set; }
    }
}
