using System;
using System.Collections.Generic;

namespace KTransport.API.DTOs
{
    /// <summary>Mirror of <see cref="Models.MenuItem"/> for the admin API surface.</summary>
    public class MenuItemDto
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string? ParentKey { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Path { get; set; }
        public string? Icon { get; set; }
        public string? PermissionKey { get; set; }
        public string? Badge { get; set; }
        public int DisplayOrder { get; set; }
        public string? VisibilityRule { get; set; }
        public bool IsActive { get; set; }
    }

    public class MenuParityReportTenantGroupDto
    {
        public Guid TenantId { get; set; }
        public int MismatchCount { get; set; }
        public DateTime LastMismatchAt { get; set; }
        public List<List<string>> SampleCodeOnlyKeys { get; set; } = new();
        public List<List<string>> SampleTablesOnlyKeys { get; set; } = new();
    }

    public class MenuParityReportDto
    {
        public DateTime GeneratedAt { get; set; }
        public DateTime WindowStart { get; set; }
        public int TotalMismatches { get; set; }
        public List<MenuParityReportTenantGroupDto> ByTenant { get; set; } = new();
    }
}
