using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    /// <summary>
    /// Phase 4 of the RBAC hardening plan: tenant-scoped custom role management.
    /// Lets a tenant admin create/edit/deactivate roles beyond the 7 seeded
    /// system roles and set each role's permission grants. System roles
    /// (IsSystem = true) are managed by <see cref="ITenantService"/> /
    /// <c>SystemRolePermissionMatrix</c> and are read-only through this service.
    /// </summary>
    public interface IRoleManagementService
    {
        Task<List<RoleDto>> GetRolesAsync(Guid tenantId);

        /// <summary>
        /// The pages/actions the tenant admin may assign to a role — scoped to
        /// the tenant's entitled modules (the paid ceiling). A tenant can never
        /// grant a role a page it did not pay for.
        /// </summary>
        Task<AssignablePermissionsDto> GetAssignablePermissionsAsync(Guid tenantId);

        Task<RoleDetailDto?> GetRoleAsync(Guid tenantId, int roleId);

        Task<(bool Success, string Message, RoleDetailDto? Data)> CreateRoleAsync(
            Guid tenantId, CreateRoleRequest request, int? actingUserId);

        Task<(bool Success, string Message, RoleDetailDto? Data)> UpdateRoleAsync(
            Guid tenantId, int roleId, UpdateRoleRequest request, int? actingUserId);

        Task<(bool Success, string Message)> DeactivateRoleAsync(
            Guid tenantId, int roleId, int? actingUserId);
    }
}
