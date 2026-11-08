using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// Per-tenant auto-posting switch and defaults (ADR accounting-autoposting-phase2.md Q6).
    /// A 1:1 extension table keyed by <see cref="TenantId"/> — a separate concern from
    /// <c>tenant</c> / <c>tenant_modules</c> (DATABASE.md rule 2) that keeps the tenant root slim
    /// (rule 1). Auto-posting is DEFAULT OFF: nothing posts until an accountant opts in.
    /// </summary>
    public class AccountingSettings : ITenantScopedEntity
    {
        /// <summary>Primary key AND tenant scope (1:1 with the tenant).</summary>
        public Guid TenantId { get; set; }

        /// <summary>DEFAULT OFF (ADR Q6). The dispatcher skips a tenant whose flag is false.</summary>
        public bool AutoPostingEnabled { get; set; }

        /// <summary>Which BANK ledger receives card/UPI/NEFT settlements (optional override).</summary>
        public long? DefaultBankAccountId { get; set; }

        public DateTime? EnabledAt { get; set; }

        public int? EnabledBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
