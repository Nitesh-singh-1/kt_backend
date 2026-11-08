using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// RBAC clean-slate (ADR rbac-menu-consolidation-clean-slate): per-tenant
    /// page-level entitlement whitelist. NO rows for a module = ALL its pages
    /// are entitled. Rows present = only the listed page_keys are entitled.
    /// Append-only — disabling a page stamps <see cref="EnabledUntil"/>.
    /// </summary>
    public class TenantModulePage : ITenantScopedEntity
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public virtual Tenant? Tenant { get; set; }
        public int ModuleId { get; set; }
        public virtual Module? Module { get; set; }

        /// <summary>→ menu_items.key (the owning page).</summary>
        public string PageKey { get; set; } = string.Empty;

        public DateTime EnabledFrom { get; set; } = DateTime.UtcNow;
        public DateTime? EnabledUntil { get; set; }
        public int? EnabledBy { get; set; }
    }
}
