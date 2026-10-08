using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// Per-tenant per-report ON/OFF. TASK-044 Phase 3 dropped the embedded
    /// <c>reports[]</c> array from menu_entitlements_json — this table is now the
    /// sole source.
    /// </summary>
    public class TenantReportEntitlement : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string ReportKey { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int? UpdatedBy { get; set; }
    }
}
