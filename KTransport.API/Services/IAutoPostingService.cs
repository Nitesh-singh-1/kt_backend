using System.Threading;
using System.Threading.Tasks;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    /// <summary>
    /// Phase 2 auto-posting ENGINE (ADR accounting-autoposting-phase2.md). Turns an operational
    /// event into a balanced double-entry voucher linked to its source via source_type + source_id.
    /// Idempotent on the LIVE voucher per (tenant, source_type, source_id) — a service pre-check plus
    /// the partial unique index. Reversals flow through <see cref="IVoucherService.ReverseAsync"/>
    /// (posted vouchers are immutable; edits = reverse+repost, voids = reverse only).
    /// </summary>
    public interface IAutoPostingService
    {
        /// <summary>
        /// Writes ONE outbox row (guarded: a failure is logged and swallowed so the operational
        /// write is never broken). Called by the source-service hooks right after their write.
        /// </summary>
        Task EnqueueAsync(string sourceType, long sourceId, AutoPostOperation op);

        /// <summary>Build + post one balanced voucher from the source. No-op if a live voucher already exists.</summary>
        Task PostFromSourceAsync(string sourceType, long sourceId);

        /// <summary>Reverse the live voucher (if any) + post a fresh one from the current source state.</summary>
        Task RepostFromSourceAsync(string sourceType, long sourceId);

        /// <summary>Reverse the live voucher (if any) and do NOT repost (void / delete).</summary>
        Task ReverseFromSourceAsync(string sourceType, long sourceId);

        /// <summary>Idempotently seeds the well-known system leaf ledgers for the current tenant.</summary>
        Task EnsureWellKnownLedgersAsync();

        /// <summary>True when the current tenant has opted in (default OFF — ADR Q6).</summary>
        Task<bool> IsAutoPostingEnabledAsync();

        /// <summary>
        /// Drains the current tenant's Pending queue rows (dispatcher worker body). Skips entirely
        /// and leaves rows Pending when the tenant has auto-posting OFF. Closed period -> Deferred;
        /// posting error -> Failed; transient error -> Attempts++ (Failed at the cap). Returns the
        /// number of rows posted/reversed successfully.
        /// </summary>
        Task<int> DrainPendingForCurrentTenantAsync(int maxAttempts, CancellationToken cancellationToken = default);

        /// <summary>The live (non-reversed, source-bearing) voucher id for a source, or null.</summary>
        Task<long?> GetLiveVoucherIdAsync(string sourceType, long sourceId);
    }
}
