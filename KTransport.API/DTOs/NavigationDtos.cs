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
        public List<string> EnabledMenuKeys { get; set; } = new();
        public List<ReportEntitlementItemDto> Reports { get; set; } = new();
        public string? RoleOverridesJson { get; set; }
    }
}
