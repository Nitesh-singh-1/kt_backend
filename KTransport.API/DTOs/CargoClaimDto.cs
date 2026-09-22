using System;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    public class CargoClaimDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string ClaimNo { get; set; } = null!;
        public long ShipmentId { get; set; }
        public string? ShipmentNo { get; set; }
        public long? PartyId { get; set; }
        public string? PartyName { get; set; }
        public ClaimType ClaimType { get; set; }
        public string ClaimTypeName => ClaimType.ToString();
        public decimal ClaimAmount { get; set; }
        public decimal SettledAmount { get; set; }
        public ClaimStatus Status { get; set; }
        public string StatusName => Status.ToString();
        public DateOnly ClaimDate { get; set; }
        public DateOnly? SettledDate { get; set; }
        public string Description { get; set; } = null!;
        public string? InvestigationNotes { get; set; }
        public string? SettlementRemarks { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateClaimRequest
    {
        public string? ClaimNo { get; set; }
        public long ShipmentId { get; set; }
        public long? PartyId { get; set; }
        public ClaimType ClaimType { get; set; } = ClaimType.Damage;
        public decimal ClaimAmount { get; set; }
        public DateOnly? ClaimDate { get; set; }
        public string Description { get; set; } = null!;
    }

    public class SettleClaimRequest
    {
        public ClaimStatus Status { get; set; } = ClaimStatus.Settled;
        public decimal SettledAmount { get; set; }
        public string? SettlementRemarks { get; set; }
        public string? InvestigationNotes { get; set; }
    }
}
