using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class AuditLog : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public int? UserId { get; set; }

        public virtual User? User { get; set; }

        public string? Username { get; set; }

        public string Action { get; set; } = null!; // e.g. "Login", "LoginFailed", "PasswordResetRequested", "UserCreated", "PlanChanged"

        public bool Success { get; set; } = true;

        public string? EntityType { get; set; }

        public string? EntityId { get; set; }

        public string? Details { get; set; }

        public string? IpAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
