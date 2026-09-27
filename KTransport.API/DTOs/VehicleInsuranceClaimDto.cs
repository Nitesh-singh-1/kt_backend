using System;
using System.ComponentModel.DataAnnotations;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    public class VehicleInsuranceClaimDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public long? VehicleId { get; set; }
        public string? VehicleNo { get; set; }
        public string? ClaimNo { get; set; }
        public string? InsurerName { get; set; }
        public string? PolicyNo { get; set; }
        public string? ClaimType { get; set; }
        public DateOnly? IncidentDate { get; set; }
        public DateOnly ClaimDate { get; set; }
        public decimal ClaimAmount { get; set; }
        public decimal ApprovedAmount { get; set; }
        public VehicleClaimStatus Status { get; set; }
        public string StatusName => Status.ToString();
        public string? SurveyorName { get; set; }
        public string? Remarks { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateVehicleInsuranceClaimRequest
    {
        public long? VehicleId { get; set; }

        [Required(ErrorMessage = "Vehicle number is required.")]
        public string VehicleNo { get; set; } = null!;

        public string? ClaimNo { get; set; }
        public string? InsurerName { get; set; }
        public string? PolicyNo { get; set; }
        public string? ClaimType { get; set; }
        public DateOnly? IncidentDate { get; set; }
        public DateOnly? ClaimDate { get; set; }
        public decimal ClaimAmount { get; set; } = 0;
        public decimal ApprovedAmount { get; set; } = 0;
        public VehicleClaimStatus Status { get; set; } = VehicleClaimStatus.Filed;
        public string? SurveyorName { get; set; }
        public string? Remarks { get; set; }
    }

    public class UpdateVehicleInsuranceClaimRequest
    {
        public long? VehicleId { get; set; }
        public string? VehicleNo { get; set; }
        public string? ClaimNo { get; set; }
        public string? InsurerName { get; set; }
        public string? PolicyNo { get; set; }
        public string? ClaimType { get; set; }
        public DateOnly? IncidentDate { get; set; }
        public DateOnly? ClaimDate { get; set; }
        public decimal? ClaimAmount { get; set; }
        public decimal? ApprovedAmount { get; set; }
        public VehicleClaimStatus? Status { get; set; }
        public string? SurveyorName { get; set; }
        public string? Remarks { get; set; }
        public bool? IsActive { get; set; }
    }
}
