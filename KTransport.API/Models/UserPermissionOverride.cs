using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// TASK-040 Phase 1: per-user grant or revocation on top of role.
    /// APPEND-ONLY — never UPDATE IsGranted/PermissionKey/UserId.
    /// TASK-044 Phase 3 dropped the legacy <c>menu_entitlements_json.userOverrides</c>
    /// blob — this table is the sole source, with a REAL FK to users.id.
    /// </summary>
    public class UserPermissionOverride : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        /// <summary>Real FK to users.id (not a free-text name).</summary>
        public int UserId { get; set; }

        public virtual User? User { get; set; }

        public string PermissionKey { get; set; } = string.Empty;

        /// <summary>true = extra grant; false = explicit revoke of a role-inherited grant.</summary>
        public bool IsGranted { get; set; } = true;

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        public int? GrantedBy { get; set; }

        public long? SupersededBy { get; set; }

        public string? RevokeReason { get; set; }
    }
}
