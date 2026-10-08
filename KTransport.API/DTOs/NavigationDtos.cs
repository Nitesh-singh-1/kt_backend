using System;
using System.Collections.Generic;

namespace KTransport.API.DTOs
{
    public class DynamicMenuItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Path { get; set; }
        public string? Icon { get; set; }
        public string? PermissionKey { get; set; }
        public string? Badge { get; set; }
        public List<DynamicMenuItemDto> Children { get; set; } = new();
    }

    public class ReportEntitlementItemDto
    {
        public string ReportKey { get; set; } = string.Empty; // e.g. "booking_register", "tax_summary", "trip_profitability", "party_outstanding", "vendor_payables"
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "General"; // Financial, Operational, Fleet, Tax
        public string Path { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }

    public class TenantMenuEntitlementsDto
    {
        public Guid TenantId { get; set; }
        public string? PlanTier { get; set; } = "Enterprise"; // Starter, Professional, Enterprise, Custom
        public List<string> EnabledMenuKeys { get; set; } = new();
        public List<ReportEntitlementItemDto> Reports { get; set; } = new();

        /// <summary>
        /// TASK-044 Phase 3: structured role overrides. Dictionary of role → list of feature keys.
        /// Replaces the legacy RoleOverridesJson string field for write path.
        /// </summary>
        public Dictionary<string, List<string>>? RoleOverrides { get; set; }

        /// <summary>
        /// TASK-044 Phase 3: structured user overrides. Dictionary of user id/username → list of feature keys.
        /// Replaces the legacy UserOverridesJson string field for write path.
        /// </summary>
        public Dictionary<string, List<string>>? UserOverrides { get; set; }

        [Obsolete("Removed in Phase 3 of entitlements normalization. Use RoleOverrides / UserOverrides structured properties instead. Will be removed in next release.")]
        public string? RoleOverridesJson { get; set; }

        [Obsolete("Removed in Phase 3 of entitlements normalization. Use RoleOverrides / UserOverrides structured properties instead. Will be removed in next release.")]
        public string? UserOverridesJson { get; set; }
    }
}
