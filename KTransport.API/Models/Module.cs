using System;

namespace KTransport.API.Models
{
    /// <summary>
    /// TASK-046 Phase 1: Global modules catalog (product-wide).
    /// Not tenant-scoped — modules are the authoritative list of top-level
    /// product capabilities (bilty, pod, billing, etc.). Per-tenant enablement
    /// lives in <see cref="TenantModule"/>.
    /// </summary>
    public class Module
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsPremium { get; set; }
        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
