using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// Phase 2 auto-posting TRANSACTIONAL OUTBOX / work queue row (ADR
    /// accounting-autoposting-phase2.md Q1, Option A3). A source-service hook inserts ONE row in
    /// the same unit of work as the operational write; a background dispatcher drains it and asks
    /// <see cref="Services.IAutoPostingService"/> to build+post the balanced voucher. This is a
    /// prunable work table — NOT business history (DATABASE.md rule 4: the voucher is the
    /// append-only history). Kept deliberately slim (≤ 20 columns, DATABASE.md rule 1).
    /// </summary>
    public class AutoPostingQueueRow : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        /// <summary>Canonical operational source document type, e.g. "Invoice" (ADR Q3 table).</summary>
        public string SourceType { get; set; } = null!;

        /// <summary>Operational source document id (see the per-source id space in the contract).</summary>
        public long SourceId { get; set; }

        public AutoPostOperation Operation { get; set; } = AutoPostOperation.Post;

        public AutoPostStatus Status { get; set; } = AutoPostStatus.Pending;

        /// <summary>Retry counter; the dispatcher stops retrying a Failed row past the cap.</summary>
        public int Attempts { get; set; }

        public string? LastError { get; set; }

        public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedAt { get; set; }
    }

    /// <summary>What the dispatcher should do with the source document (ADR Q4).</summary>
    public enum AutoPostOperation
    {
        /// <summary>Build + post a fresh balanced voucher (idempotent on the live voucher).</summary>
        Post,

        /// <summary>Reverse the live voucher + post a new one from the current source state.</summary>
        Repost,

        /// <summary>Reverse the live voucher and do NOT repost (void / delete).</summary>
        Reverse
    }

    /// <summary>Lifecycle of a queue row.</summary>
    public enum AutoPostStatus
    {
        /// <summary>Waiting to be drained (or parked because the tenant has auto-posting OFF).</summary>
        Pending,

        /// <summary>Posted / reversed successfully.</summary>
        Done,

        /// <summary>Containing financial period is Closed — parked for an accountant to resolve (ADR Q5).</summary>
        Deferred,

        /// <summary>Posting threw (balancing / missing ledger / bad data); surfaced in reconciliation.</summary>
        Failed
    }
}
