using System;
using System.Collections.Generic;

namespace KTransport.API.DTOs
{
    // ---- §A catalog ----
    public class PageCatalogItemDto
    {
        public string PageKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Path { get; set; }
        public string? ParentKey { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ModuleWithPagesDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsPremium { get; set; }
        public int DisplayOrder { get; set; }
        public List<PageCatalogItemDto> Pages { get; set; } = new();
    }

    // ---- §C plans ----
    public class PlanDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal MonthlyPriceInr { get; set; }
        public int? MaxUsers { get; set; }
        public int? MaxVehicles { get; set; }
        public int? MaxMonthlyShipments { get; set; }
        public int? StorageLimitMb { get; set; }
        public List<string> ModuleCodes { get; set; } = new();
        public Dictionary<string, List<string>>? PageRestrictions { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // ---- §B plan assignment ----
    public class AssignPlanRequest
    {
        public string PlanCode { get; set; } = string.Empty;
    }

    // ---- §G tenant console ----
    public class TenantSummaryDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? PlanCode { get; set; }
        public int ModuleCount { get; set; }
        public int UserCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TenantDetailModuleDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool PageRestricted { get; set; }
    }

    public class TenantDetailRoleDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsSystem { get; set; }
        public int UserCount { get; set; }
    }

    public class TenantDetailUserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public List<string> RoleCodes { get; set; } = new();
    }

    public class TenantDetailDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? PlanCode { get; set; }
        public List<TenantDetailModuleDto> Modules { get; set; } = new();
        public List<TenantDetailRoleDto> Roles { get; set; } = new();
        public List<TenantDetailUserDto> Users { get; set; } = new();
    }

    public class SetUserRolesRequest
    {
        public List<string> RoleCodes { get; set; } = new();
    }
}
