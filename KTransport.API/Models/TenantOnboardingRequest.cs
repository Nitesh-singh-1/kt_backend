using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using KTransport.API.DTOs;

namespace KTransport.API.Models
{
    public class TenantOnboardingRequest
    {
        [Required(ErrorMessage = "Organization name is required.")]
        [StringLength(200, MinimumLength = 2)]
        public string OrganizationName { get; set; } = null!;

        [Required(ErrorMessage = "Organization code is required.")]
        [StringLength(20, MinimumLength = 2)]
        [RegularExpression(@"^[A-Z0-9_-]+$", ErrorMessage = "Organization code may contain only A-Z, 0-9, underscore or hyphen.")]
        public string OrganizationCode { get; set; } = null!;

        [Required(ErrorMessage = "Admin username is required.")]
        [StringLength(50, MinimumLength = 3)]
        public string AdminUsername { get; set; } = null!;

        [Required(ErrorMessage = "Admin password is required.")]
        [StringLength(200, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        public string AdminPassword { get; set; } = null!;

        [Required(ErrorMessage = "Admin full name is required.")]
        [StringLength(150)]
        public string AdminFullName { get; set; } = null!;

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Mobile number must be a valid 10-digit Indian mobile number.")]
        public string? AdminMobile { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [StringLength(150)]
        public string? AdminEmail { get; set; }

        public string? AdminRole { get; set; } = "admin";
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

    public class TenantUserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Mobile { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public bool IsAdmin { get; set; }
    }

    public class SetUserRoleRequest
    {
        public int UserId { get; set; }
        public string Role { get; set; } = "admin"; // "admin" or "SUB_USER"
    }

    public class TenantStatusUpdateDto
    {
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// TASK-046 Phase 1: payload for the platform-admin-only onboarding endpoint
    /// POST /api/admin/tenants. Unlike the public /api/tenant/onboard which forces
    /// the admin user's role to 'admin', this endpoint respects
    /// <see cref="AdminRoleCode"/> and takes explicit module enablement.
    /// </summary>
    public class TenantAdminOnboardRequest
    {
        [Required]
        [StringLength(200, MinimumLength = 2)]
        public string OrganizationName { get; set; } = null!;

        [Required]
        [StringLength(20, MinimumLength = 2)]
        [RegularExpression(@"^[A-Z0-9_-]+$")]
        public string OrganizationCode { get; set; } = null!;

        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string AdminUsername { get; set; } = null!;

        [Required]
        [StringLength(200, MinimumLength = 6)]
        public string AdminPassword { get; set; } = null!;

        [Required]
        [StringLength(150)]
        public string AdminFullName { get; set; } = null!;

        [RegularExpression(@"^[6-9]\d{9}$")]
        public string? AdminMobile { get; set; }

        [EmailAddress]
        [StringLength(150)]
        public string? AdminEmail { get; set; }

        /// <summary>Role code for the admin user; platform admin may pick any seeded system role or a custom role code.</summary>
        public string AdminRoleCode { get; set; } = "admin";

        public string PlanTier { get; set; } = "Starter";

        /// <summary>Module codes to enable for the new tenant (writes tenant_modules rows).</summary>
        public List<string> EnabledModuleCodes { get; set; } = new();

        public List<string>? EnabledReportKeys { get; set; }
    }

    /// <summary>TASK-046 Phase 1: GET /api/admin/roles/system-templates row.</summary>
    public class SystemRoleTemplateDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
