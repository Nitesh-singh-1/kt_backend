using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// TASK-046 Phase 1: user ↔ role M2M. APPEND-ONLY — revoking a role
    /// stamps <see cref="RevokedAt"/> + <see cref="RevokedBy"/>; re-assigning
    /// the same role creates a new row. A partial unique index enforces at
    /// most one ACTIVE assignment per (tenant, user, role).
    /// </summary>
    public class UserRole : ITenantScopedEntity
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public virtual Tenant? Tenant { get; set; }
        public int UserId { get; set; }
        public virtual User? User { get; set; }
        public int RoleId { get; set; }
        public virtual Role? Role { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public int? AssignedBy { get; set; }
        public DateTime? RevokedAt { get; set; }
        public int? RevokedBy { get; set; }
        public string? RevokeReason { get; set; }
    }
}
