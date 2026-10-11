using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// TASK-046 Phase 1: per-tenant role catalog. Seven system roles are seeded
    /// per tenant on onboarding (<c>IsSystem=true</c>). Tenants may create
    /// custom roles beyond the seeded set. Not deleted — <see cref="IsActive"/>
    /// soft-flag only.
    /// </summary>
    public class Role : ITenantScopedEntity
    {
        public int Id { get; set; }
        public Guid TenantId { get; set; }
        public virtual Tenant? Tenant { get; set; }

        /// <summary>Stable identifier used in code, lower_snake_case (e.g. "admin", "operations_manager").</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Display name — tenant-editable for custom roles.</summary>
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; } = true;
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
