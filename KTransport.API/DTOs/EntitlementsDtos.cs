using System;
using System.Collections.Generic;

namespace KTransport.API.DTOs
{
    // ---------- TASK-040 Phase 1 DTOs ----------

    public class PermissionDto
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string FeatureKey { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class TenantEntitlementSubscriptionDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string PlanTier { get; set; } = "Enterprise";
        public List<string> EnabledFeatureKeys { get; set; } = new();
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
    }

    public class TenantReportEntitlementDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string ReportKey { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    public class RolePermissionDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string PermissionKey { get; set; } = string.Empty;
        public DateTime GrantedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
    }

    public class UserPermissionOverrideDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public int UserId { get; set; }
        public string PermissionKey { get; set; } = string.Empty;
        public bool IsGranted { get; set; }
        public DateTime GrantedAt { get; set; }
    }

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
}
