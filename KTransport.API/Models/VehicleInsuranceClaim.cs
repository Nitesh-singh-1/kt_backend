using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>An insurance claim raised against a fleet vehicle (accident / own-damage / theft, etc.).</summary>
    public class VehicleInsuranceClaim : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public long? VehicleId { get; set; }

        public virtual Vehicle? Vehicle { get; set; }

        public string? VehicleNo { get; set; }

        public string? ClaimNo { get; set; }

        public string? InsurerName { get; set; }

        public string? PolicyNo { get; set; }

        public string? ClaimType { get; set; } // Accident, Own Damage, Third Party, Theft, Fire, Other

        public DateOnly? IncidentDate { get; set; }

        public DateOnly ClaimDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        public decimal ClaimAmount { get; set; } = 0;

        public decimal ApprovedAmount { get; set; } = 0;

        public VehicleClaimStatus Status { get; set; } = VehicleClaimStatus.Filed;

        public string? SurveyorName { get; set; }

        public string? Remarks { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
