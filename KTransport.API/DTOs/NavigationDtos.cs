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
        public string ReportKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public string Path { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// RBAC clean-slate (contract §B): the ONE tenant-entitlement shape for both
    /// GET (TenantEntitlements) and PUT (UpdateTenantEntitlements). The legacy
    /// EnabledMenuKeys / RoleOverrides / UserOverrides / PlanTier fields are GONE.
    /// </summary>
    public class TenantMenuEntitlementsDto
    {
        public Guid TenantId { get; set; }

        /// <summary>The plan currently assigned (null = custom).</summary>
        public string? PlanCode { get; set; }

        /// <summary>Active tenant_modules (canonical codes).</summary>
        public List<string> ModuleCodes { get; set; } = new();

        /// <summary>
        /// Present ONLY for modules the platform admin narrowed. Key = module
        /// code, value = whitelisted page keys. A module absent here = ALL pages.
        /// </summary>
        public Dictionary<string, List<string>> PageRestrictions { get; set; } = new();

        public List<ReportEntitlementItemDto> Reports { get; set; } = new();
    }
}
