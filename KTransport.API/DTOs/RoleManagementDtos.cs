using System.Collections.Generic;

namespace KTransport.API.DTOs
{
    /// <summary>Summary row for the role list screen.</summary>
    public class RoleDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
        public int PermissionCount { get; set; }
        public int AssignedUserCount { get; set; }
    }

    /// <summary>Full role detail including its granted permission keys.</summary>
    public class RoleDetailDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
        public List<string> PermissionKeys { get; set; } = new();
    }

    public class CreateRoleRequest
    {
        /// <summary>Stable lower_snake_case identifier, unique per tenant (e.g. "dispatch_lead").</summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        /// <summary>Catalog permission keys to grant (e.g. "consignments.create.create").</summary>
        public List<string> PermissionKeys { get; set; } = new();
    }

    public class UpdateRoleRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }

        /// <summary>
        /// The complete desired permission set for the role. The service
        /// reconciles against the current grants — missing keys are granted,
        /// removed keys are soft-revoked. Null means "leave permissions as-is".
        /// </summary>
        public List<string>? PermissionKeys { get; set; }
    }

    // ---- Assignable-permission matrix (bounded by what the tenant paid for) ----

    /// <summary>
    /// The pages × actions a tenant admin is allowed to put on a role — i.e. the
    /// tenant's entitled modules only (the paid ceiling). Drives the role-editor
    /// matrix on the frontend.
    /// </summary>
    public class AssignablePermissionsDto
    {
        public List<AssignableModuleDto> Modules { get; set; } = new();
    }

    public class AssignableModuleDto
    {
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public List<AssignablePageDto> Pages { get; set; } = new();
    }

    public class AssignablePageDto
    {
        /// <summary>The feature/page key, e.g. "consignments.create".</summary>
        public string FeatureKey { get; set; } = string.Empty;
        public List<AssignableActionDto> Actions { get; set; } = new();
    }

    public class AssignableActionDto
    {
        /// <summary>Full permission key to grant, e.g. "consignments.create.delete".</summary>
        public string Key { get; set; } = string.Empty;
        /// <summary>Action label, e.g. "View", "Create", "Edit", "Delete".</summary>
        public string Action { get; set; } = string.Empty;
    }
}
