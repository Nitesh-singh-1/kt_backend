using System;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    public class PartyDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
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
        public string PartyTypeName => PartyType.ToString();
        public PaymentTerm DefaultPaymentTerm { get; set; } = PaymentTerm.ToPay;
        public string DefaultPaymentTermName => DefaultPaymentTerm.ToString();
        public decimal CreditLimit { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreatePartyRequest
    {
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
    }

    public class UpdatePartyRequest
    {
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
    }

    public class PartyLookupDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Code { get; set; }
        public string? GstNo { get; set; }
        public string? Mobile { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        public PartyType PartyType { get; set; }
        public PaymentTerm DefaultPaymentTerm { get; set; }
    }
}
