using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// TASK-046 Phase 1: per-tenant module enablement (replaces the
    /// <c>tenant_entitlement_subscriptions.enabled_feature_keys</c> text[] as
    /// the authoritative source going forward). Append-only — disabling a
    /// module sets <see cref="EnabledUntil"/>; re-enabling INSERTs a new row.
    /// The legacy text[] column is kept populated via dual-write for one-release
    /// rollback safety.
    /// </summary>
    public class TenantModule : ITenantScopedEntity
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public virtual Tenant? Tenant { get; set; }
        public int ModuleId { get; set; }
        public virtual Module? Module { get; set; }
        public bool IsEnabled { get; set; } = true;
        public DateTime EnabledFrom { get; set; } = DateTime.UtcNow;
        public DateTime? EnabledUntil { get; set; }
        public int? EnabledBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
