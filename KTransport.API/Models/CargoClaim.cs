using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class CargoClaim : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string ClaimNo { get; set; } = null!; // e.g. CLM-2026-0001

        public long ShipmentId { get; set; }

        public virtual Shipment? Shipment { get; set; }

        public string? ShipmentNo { get; set; }

        public long? PartyId { get; set; }

        public virtual Party? Party { get; set; }

        public string? PartyName { get; set; }

        public ClaimType ClaimType { get; set; } = ClaimType.Damage;

        public decimal ClaimAmount { get; set; } = 0;

        public decimal SettledAmount { get; set; } = 0;

        public ClaimStatus Status { get; set; } = ClaimStatus.Reported;

        public DateOnly ClaimDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        public DateOnly? SettledDate { get; set; }

        public string Description { get; set; } = null!;

        public string? InvestigationNotes { get; set; }

        public string? SettlementRemarks { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
