using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class Driver : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string Name { get; set; } = null!;

        public string? Mobile { get; set; }

        public string? LicenseNo { get; set; }

        public DateOnly? LicenseValidUntil { get; set; }

        public string? AadharNo { get; set; }

        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
