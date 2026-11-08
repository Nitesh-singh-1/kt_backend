using System;
using System.Collections.Generic;

namespace KTransport.API.DTOs
{
    // ---------- TASK-040 Phase 1 DTOs ----------

    // RBAC clean-slate: the legacy PermissionDto (feature_key) /
    // TenantEntitlementSubscriptionDto (enabled_feature_keys) /
    // RolePermissionDto (role_name, permission_key) / UserPermissionOverrideDto
    // (permission_key) DTOs are deleted — nothing emits those shapes any more.

    public class BackfillOrphanDto
    {
        public Guid TenantId { get; set; }
        public string OrphanKey { get; set; } = string.Empty;
        public List<string> AttemptedFeatureKeys { get; set; } = new();
    }

    public class BackfillSummaryDto
    {
        public int TenantsProcessed { get; set; }
        public int SubscriptionsInserted { get; set; }
        public int ReportEntitlementsInserted { get; set; }
        public int RolePermissionsInserted { get; set; }
        public int UserOverridesInserted { get; set; }
        public int OrphansSkipped { get; set; }
        public List<BackfillOrphanDto> OrphanDetails { get; set; } = new();
    }

    public class ParityReportTenantGroupDto
    {
        public Guid TenantId { get; set; }
        public int MismatchCount { get; set; }
        public DateTime LastMismatchAt { get; set; }
        public List<string> SampleJsonOnlyKeys { get; set; } = new();
        public List<string> SampleTablesOnlyKeys { get; set; } = new();
    }

    public class ParityReportDto
    {
        public DateTime GeneratedAt { get; set; }
        public DateTime WindowStart { get; set; }
        public int TotalMismatches { get; set; }
        public List<ParityReportTenantGroupDto> ByTenant { get; set; } = new();
    }

    /// <summary>
    /// Phase 1 keystone follow-up: the authoritative module catalog the
    /// platform-admin /clients UI renders its assign/revoke checklist from.
    /// Served from the live <c>modules</c> table so the UI can never drift from
    /// the DB (no more hardcoded frontend list that silently omits a real
    /// module — e.g. the paid <c>accounting</c> add-on — and makes it
    /// unprovisionable). See .agent/specs/decisions/menu-admin-visibility-keystone.md.
    /// </summary>
    public class ModuleCatalogItemDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsPremium { get; set; }
        public int DisplayOrder { get; set; }
    }
}
