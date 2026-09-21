using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public enum PartyType
    {
        Both = 0,
        Consignor = 1,
        Consignee = 2,
        Transporter = 3,
        Agent = 4
    }

    public class Party : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string Name { get; set; } = null!;

        public string? Code { get; set; }

        public string? GstNo { get; set; }

        public string? PanNo { get; set; }

        public string? Mobile { get; set; }

        public string? Phone { get; set; }

        public string? Email { get; set; }

        public string? Address { get; set; }

        public string? City { get; set; }

        public string? State { get; set; }

        public string? Pincode { get; set; }

        public PartyType PartyType { get; set; } = PartyType.Both;

        public PaymentTerm DefaultPaymentTerm { get; set; } = PaymentTerm.ToPay;

        public decimal CreditLimit { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
