using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    /// <summary>
    /// HTTP-facing read/admin surface for Phase 2 auto-posting: the on/off settings, the queue
    /// (list + retry), the reconciliation report, and the source -> voucher drill-through. The actual
    /// posting is done by <see cref="IAutoPostingService"/> + the dispatcher; this service never posts.
    /// </summary>
    public interface IAutoPostingManagementService
    {
        Task<AutoPostingSettingsDto> GetSettingsAsync();
        Task<AutoPostingSettingsDto> UpdateSettingsAsync(UpdateAutoPostingSettingsRequest request, int? userId);

        Task<PagedResult<AutoPostingQueueItemDto>> GetQueueAsync(string? status, DateOnly? from, DateOnly? to, int page, int pageSize);

        /// <summary>Re-queues a Failed/Deferred row. Returns null (404) if missing; throws 409 if not retryable.</summary>
        Task<AutoPostingQueueItemDto?> RetryAsync(long id);

        Task<ReconciliationReportDto> GetReconciliationAsync(DateOnly? from, DateOnly? to);

        /// <summary>The live voucher + any reversals linked to a source document, newest first.</summary>
        Task<List<VoucherDto>> GetVouchersBySourceAsync(string sourceType, long sourceId);
    }
}
