using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// A pending invitation for someone to join a tenant with a preset role and feature set.
    /// The invitee receives an emailed link and sets their own username/password on accept —
    /// replacing the admin directly setting another person's password.
    /// </summary>
    public class Invitation : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string Email { get; set; } = null!;

        public string Role { get; set; } = "SUB_USER";

        /// <summary>JSON array of feature keys to grant on accept (validated against the org's subscription).</summary>
        public string AssignedFeaturesJson { get; set; } = "[]";

        /// <summary>High-entropy, single-use token embedded in the accept link.</summary>
        public string Token { get; set; } = null!;

        public string Status { get; set; } = "Pending"; // Pending | Accepted | Revoked

        public DateTime ExpiresAt { get; set; }

        public int? InvitedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? AcceptedAt { get; set; }
    }
}
