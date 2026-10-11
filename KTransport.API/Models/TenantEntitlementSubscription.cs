using System;
using System.Collections.Generic;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    /// <summary>
    /// Per-tenant feature-entitlement grant (what the tenant has paid for).
    /// TASK-044 Phase 3 dropped <c>menu_entitlements_json.enabledMenuKeys</c> —
    /// this row is now the sole source.
    ///
    /// NB: Contract called the table "tenant_subscriptions" but that name is
    /// ALREADY taken by the billing-plan model <see cref="TenantSubscription"/>.
    /// Renamed here to <c>tenant_entitlement_subscriptions</c> to avoid the clash
    /// (noted as a contract gap in backend.md).
    /// </summary>
    public class TenantEntitlementSubscription : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        /// <summary>Starter, Professional, Enterprise, Custom.</summary>
        public string PlanTier { get; set; } = "Enterprise";

        /// <summary>Postgres text[] of feature keys (e.g. ["billing","billing.bill_book"]).</summary>
        public List<string> EnabledFeatureKeys { get; set; } = new();

        public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

        /// <summary>Null = currently active.</summary>
        public DateTime? EffectiveUntil { get; set; }

        public int? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
