using System;
using System.ComponentModel.DataAnnotations;
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
        public string? ContactPerson { get; set; }
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
        [Required(ErrorMessage = "Company / Party Name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Party name must be between 2 and 150 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\.\,\&\-\'\(\)\/\#]+$", ErrorMessage = "Party name contains invalid characters. Only letters, numbers, spaces, and . , & - ' ( ) / # are allowed.")]
        public string Name { get; set; } = null!;

        [StringLength(50, ErrorMessage = "Party code / alias cannot exceed 50 characters.")]
        public string? Code { get; set; }

        [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Invalid GSTIN format. Expected 15 characters (e.g. 27AAAAA0000A1Z5).")]
        public string? GstNo { get; set; }

        [RegularExpression(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$", ErrorMessage = "Invalid PAN format. Expected 10 alphanumeric characters (e.g. AAAAA0000A).")]
        public string? PanNo { get; set; }

        [StringLength(100, ErrorMessage = "Contact person name cannot exceed 100 characters.")]
        public string? ContactPerson { get; set; }

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Mobile number must be a valid 10-digit Indian mobile number.")]
        public string? Mobile { get; set; }

        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        public string? Email { get; set; }

        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }

        [RegularExpression(@"^\d{6}$", ErrorMessage = "Pincode must be exactly 6 digits.")]
        public string? Pincode { get; set; }

        public PartyType PartyType { get; set; } = PartyType.Both;
        public PaymentTerm DefaultPaymentTerm { get; set; } = PaymentTerm.ToPay;
        public decimal CreditLimit { get; set; } = 0;
    }

    public class UpdatePartyRequest
    {
        [Required(ErrorMessage = "Company / Party Name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Party name must be between 2 and 150 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\.\,\&\-\'\(\)\/\#]+$", ErrorMessage = "Party name contains invalid characters. Only letters, numbers, spaces, and . , & - ' ( ) / # are allowed.")]
        public string Name { get; set; } = null!;

        [StringLength(50, ErrorMessage = "Party code / alias cannot exceed 50 characters.")]
        public string? Code { get; set; }

        [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Invalid GSTIN format. Expected 15 characters (e.g. 27AAAAA0000A1Z5).")]
        public string? GstNo { get; set; }

        [RegularExpression(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$", ErrorMessage = "Invalid PAN format. Expected 10 alphanumeric characters (e.g. AAAAA0000A).")]
        public string? PanNo { get; set; }

        [StringLength(100, ErrorMessage = "Contact person name cannot exceed 100 characters.")]
        public string? ContactPerson { get; set; }

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Mobile number must be a valid 10-digit Indian mobile number.")]
        public string? Mobile { get; set; }

        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        public string? Email { get; set; }

        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }

        [RegularExpression(@"^\d{6}$", ErrorMessage = "Pincode must be exactly 6 digits.")]
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
