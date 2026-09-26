using System;

namespace KTransport.API.Models
{
    /// <summary>
    /// DB-backed, single-use verification code (e.g. password-reset OTP). Replaces the previous
    /// in-memory ConcurrentDictionary so codes survive restarts and work across multiple instances.
    /// Not tenant-scoped: it is an authentication primitive used before a tenant context exists,
    /// keyed by the globally-unique username.
    /// </summary>
    public class VerificationCode
    {
        public long Id { get; set; }

        public string Username { get; set; } = null!;

        /// <summary>What the code is for, e.g. "PasswordReset". Lets one table serve multiple flows.</summary>
        public string Purpose { get; set; } = "PasswordReset";

        /// <summary>BCrypt hash of the code — never store the plaintext OTP at rest.</summary>
        public string CodeHash { get; set; } = null!;

        public DateTime ExpiresAt { get; set; }

        public DateTime? ConsumedAt { get; set; }

        public int AttemptCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
