using System;

namespace KTransport.API.Models
{
    /// <summary>
    /// A long-lived refresh token bound to a user. Stored HASHED (SHA-256) — the raw token lives only
    /// on the client. Rotated on every use: the old row is revoked and linked to its replacement, so a
    /// stolen-then-reused token is detectable and the chain can be cut.
    /// Not tenant-scoped: an auth primitive keyed by user, used before/around token exchange.
    /// </summary>
    public class RefreshToken
    {
        public long Id { get; set; }

        public int UserId { get; set; }

        public virtual User? User { get; set; }

        /// <summary>SHA-256 hash (hex) of the raw refresh token.</summary>
        public string TokenHash { get; set; } = null!;

        public DateTime ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RevokedAt { get; set; }

        /// <summary>Hash of the token that replaced this one on rotation (audit/reuse detection).</summary>
        public string? ReplacedByHash { get; set; }

        public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;
    }
}
