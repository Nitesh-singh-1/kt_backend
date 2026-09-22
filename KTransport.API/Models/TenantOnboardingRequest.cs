using System;
using System.Collections.Generic;
using KTransport.API.DTOs;

namespace KTransport.API.Models
{
    public class TenantOnboardingRequest
    {
        public string OrganizationName { get; set; } = null!;
        public string OrganizationCode { get; set; } = null!;
        public string AdminUsername { get; set; } = null!;
        public string AdminPassword { get; set; } = null!;
        public string AdminFullName { get; set; } = null!;
        public string? AdminMobile { get; set; }
        public string? PlanTier { get; set; } = "Starter"; // Starter, Professional, Enterprise, Custom
        public List<string>? EnabledModules { get; set; } // ["dashboard", "gr", "gr.list", "gr.entry", "challan", "challan.list", "challan.entry", "reports", "system"]
        public List<string>? EnabledReportKeys { get; set; } // ["booking_register", "tax_summary", "party_outstanding", "trip_profitability", "vendor_payables"]
    }

    public class TenantOnboardingResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
        public string? OrganizationName { get; set; }
        public string? OrganizationCode { get; set; }
        public string? Token { get; set; }
        public UserDto? AdminUser { get; set; }
        public string? PlanTier { get; set; }
        public List<string>? EnabledModules { get; set; }
    }

    public class TenantAdminListItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? AdminUsername { get; set; }
        public string? AdminFullName { get; set; }
        public string? AdminMobile { get; set; }
        public int UserCount { get; set; }
        public string SubscriptionPlanTier { get; set; } = "Starter";
        public string SubscriptionStatus { get; set; } = "Active";
        public decimal MonthlyPrice { get; set; }
        public List<string> EnabledMenuKeys { get; set; } = new();
        public List<string> EnabledReportKeys { get; set; } = new();
        public int EnabledReportsCount { get; set; }
    }

    public class TenantPlanUpdateDto
    {
        public string PlanTier { get; set; } = "Starter";
    }

    public class TenantStatusUpdateDto
    {
        public bool IsActive { get; set; }
    }
}
