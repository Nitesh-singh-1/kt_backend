using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// TASK-040 Phase 1: per-tenant per-role grant.
    /// APPEND-ONLY — revocations create a new row OR update RevokedAt + RevokedBy.
    /// Never DELETE. Sole source of role grants (TASK-044 Phase 3 dropped
    /// the legacy <c>menu_entitlements_json.roleOverrides</c> blob).
    /// </summary>
    public class RolePermission : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        /// <summary>Lowercase. e.g. admin, dispatcher, billing_operator, viewer.</summary>
        public string RoleName { get; set; } = string.Empty;

        /// <summary>Semantic FK to <see cref="Permission.Key"/>; stored as string.</summary>
        public string PermissionKey { get; set; } = string.Empty;

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        public int? GrantedBy { get; set; }

        public DateTime? RevokedAt { get; set; }

        public int? RevokedBy { get; set; }

        public string? RevokeReason { get; set; }

        public long? SupersededBy { get; set; }
    }
}
