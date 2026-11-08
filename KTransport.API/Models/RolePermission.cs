using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// Per-tenant per-role grant. APPEND-ONLY — revocations stamp
    /// <see cref="RevokedAt"/>, never DELETE.
    ///
    /// RBAC clean-slate (ADR rbac-menu-consolidation-clean-slate): the legacy
    /// string columns <c>role_name</c> / <c>permission_key</c> are GONE. The
    /// authoritative keys are <see cref="RoleId"/> + <see cref="PermissionId"/>
    /// (both NOT NULL). Active unique index: (tenant_id, role_id, permission_id)
    /// WHERE revoked_at IS NULL.
    /// </summary>
    public class RolePermission : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        /// <summary>FK → <see cref="Role"/>.Id (NOT NULL).</summary>
        public int RoleId { get; set; }

        public virtual Role? Role { get; set; }

        /// <summary>FK → <see cref="Permission"/>.Id (NOT NULL).</summary>
        public int PermissionId { get; set; }

        public virtual Permission? Permission { get; set; }

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        public int? GrantedBy { get; set; }

        public DateTime? RevokedAt { get; set; }

        public int? RevokedBy { get; set; }

        public string? RevokeReason { get; set; }

        public long? SupersededBy { get; set; }
    }
}
