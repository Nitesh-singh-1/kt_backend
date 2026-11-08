using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// Per-user grant or revocation on top of role. APPEND-ONLY.
    ///
    /// RBAC clean-slate (ADR rbac-menu-consolidation-clean-slate): the legacy
    /// <c>permission_key</c> string is GONE. <see cref="PermissionId"/> is the
    /// authoritative key (NOT NULL). Current unique index:
    /// (tenant_id, user_id, permission_id) WHERE superseded_by IS NULL.
    /// </summary>
    public class UserPermissionOverride : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        /// <summary>FK → users.id.</summary>
        public int UserId { get; set; }

        public virtual User? User { get; set; }

        /// <summary>FK → <see cref="Permission"/>.Id (NOT NULL).</summary>
        public int PermissionId { get; set; }

        public virtual Permission? Permission { get; set; }

        /// <summary>true = extra grant; false = explicit revoke of a role-inherited grant.</summary>
        public bool IsGranted { get; set; } = true;

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        public int? GrantedBy { get; set; }

        public long? SupersededBy { get; set; }

        public string? RevokeReason { get; set; }
    }
}
