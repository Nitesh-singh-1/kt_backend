using System;
using System.Collections.Generic;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// A node in the chart of accounts (self-referencing tree). A node is either a
    /// GROUP (<see cref="IsGroup"/> = true, no postings allowed) or a postable LEDGER.
    /// The 28 Tally default groups are seeded per tenant with <see cref="IsSystem"/> = true
    /// and cannot be renamed or deleted by a tenant — they only accept child ledgers.
    /// Aggregate root per DATABASE.md (≤ 20 columns).
    /// </summary>
    public class Account : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        /// <summary>null for a primary group root; otherwise the parent group.</summary>
        public long? ParentId { get; set; }

        public virtual Account? Parent { get; set; }

        public virtual ICollection<Account> Children { get; set; } = new List<Account>();

        /// <summary>Stable code, unique per tenant — used by auto-posting (Phase 2) and Tally export.</summary>
        public string Code { get; set; } = null!;

        public string Name { get; set; } = null!;

        public AccountType AccountType { get; set; }

        /// <summary>Normal balance side of this account.</summary>
        public DrCr Nature { get; set; }

        /// <summary>true = group node (no direct postings); false = postable ledger.</summary>
        public bool IsGroup { get; set; }

        /// <summary>true = seeded Tally group; tenant cannot rename/delete it.</summary>
        public bool IsSystem { get; set; }

        /// <summary>Links an auto-created Sundry-Debtor/Creditor ledger back to a party/vendor (Phase 2).</summary>
        public long? SourcePartyId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
