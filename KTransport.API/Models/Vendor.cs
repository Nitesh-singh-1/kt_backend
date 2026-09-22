using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class Vendor : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string Name { get; set; } = null!; // Vendor / Transporter / Broker Name

        public string? Code { get; set; }

        public string? PanNo { get; set; }

        public string? GstNo { get; set; }

        public string? ContactPerson { get; set; }

        public string? Mobile { get; set; }

        public string? Phone { get; set; }

        public string? Email { get; set; }

        public string? Address { get; set; }

        public string? City { get; set; }

        public string? State { get; set; }

        public decimal TdsPercentage { get; set; } = 1.0m; // Standard 1% for individual/proprietor, 2% for company

        // Bank Account Details for vendor payments
        public string? BankName { get; set; }

        public string? AccountNumber { get; set; }

        public string? IfscCode { get; set; }

        public string? AccountHolderName { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
