using System;
using System.Collections.Generic;

namespace KTransport.API.DTOs
{
    /// <summary>
    /// Generic in-body paged envelope — mirrors Phase 1 <see cref="VoucherListResponse"/>
    /// ({ items, page, pageSize, totalCount }). The frontend maps total = totalCount against
    /// this exact shape; there is NO X-Total-Count header.
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    public class AutoPostingSettingsDto
    {
        public Guid TenantId { get; set; }
        public bool AutoPostingEnabled { get; set; }
        public long? DefaultBankAccountId { get; set; }
        public DateTime? EnabledAt { get; set; }
        public int? EnabledBy { get; set; }
    }

    public class UpdateAutoPostingSettingsRequest
    {
        public bool AutoPostingEnabled { get; set; }
        public long? DefaultBankAccountId { get; set; }
    }

    public class AutoPostingQueueItemDto
    {
        public long Id { get; set; }
        public string SourceType { get; set; } = null!;
        public long SourceId { get; set; }
        public string Operation { get; set; } = null!;
        public string Status { get; set; } = null!;
        public int Attempts { get; set; }
        public string? LastError { get; set; }
        public DateTime EnqueuedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        /// <summary>The live voucher currently linked to this source, if any.</summary>
        public long? LinkedVoucherId { get; set; }
    }

    public class ReconciliationCheckDto
    {
        public string Name { get; set; } = null!;
        public decimal Expected { get; set; }
        public decimal Actual { get; set; }
        public decimal Difference { get; set; }
        public bool Ok { get; set; }
    }

    public class ReconciliationReportDto
    {
        public List<ReconciliationCheckDto> Checks { get; set; } = new();
        public int PendingCount { get; set; }
        public int DeferredCount { get; set; }
        public int FailedCount { get; set; }
    }

    /// <summary>Drill-through link from an operational document to its accounting voucher(s).</summary>
    public class VoucherLinkDto
    {
        public string SourceType { get; set; } = null!;
        public long SourceId { get; set; }
        public long? VoucherId { get; set; }
        public string? VoucherNo { get; set; }
        public string? Status { get; set; }
    }
}
