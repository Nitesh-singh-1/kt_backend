using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.DTOs;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers
{
    /// <summary>
    /// Phase 4 of the RBAC hardening plan: tenant-scoped custom role management.
    /// Gated with the same granular keys as user administration. Data is
    /// tenant-isolated by the EF global query filter; a tenant admin manages
    /// only their own organization's roles.
    /// </summary>
    [ApiController]
    [Route("api/roles")]
    [Authorize]
    public class RoleController : ControllerBase
    {
        private readonly IRoleManagementService _roles;
        private readonly ITenantContext _tenantContext;

        public RoleController(IRoleManagementService roles, ITenantContext tenantContext)
        {
            _roles = roles;
            _tenantContext = tenantContext;
        }

        private int? CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        [HttpGet]
        [RequirePermission("system.users.view", "users.manage")]
        public async Task<IActionResult> GetRoles()
            => Ok(await _roles.GetRolesAsync(_tenantContext.CurrentTenantId));

        /// <summary>The pages/actions this tenant may assign to a role — bounded by the modules they paid for.</summary>
        [HttpGet("assignable-permissions")]
        [RequirePermission("system.users.view", "users.manage")]
        public async Task<IActionResult> GetAssignablePermissions()
            => Ok(await _roles.GetAssignablePermissionsAsync(_tenantContext.CurrentTenantId));

        [HttpGet("{id:int}")]
        [RequirePermission("system.users.view", "users.manage")]
        public async Task<IActionResult> GetRole(int id)
        {
            var role = await _roles.GetRoleAsync(_tenantContext.CurrentTenantId, id);
            if (role == null) return NotFound(new { success = false, message = "Role not found." });
            return Ok(role);
        }

        [HttpPost]
        [RequirePermission("system.users.create", "users.manage")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var (ok, message, data) = await _roles.CreateRoleAsync(_tenantContext.CurrentTenantId, request, CurrentUserId);
            if (!ok) return BadRequest(new { success = false, message });
            return Ok(new { success = true, message, role = data });
        }

        [HttpPut("{id:int}")]
        [RequirePermission("system.users.edit", "users.manage")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var (ok, message, data) = await _roles.UpdateRoleAsync(_tenantContext.CurrentTenantId, id, request, CurrentUserId);
            if (!ok) return BadRequest(new { success = false, message });
            return Ok(new { success = true, message, role = data });
        }

        [HttpDelete("{id:int}")]
        [RequirePermission("system.users.delete", "users.manage")]
        public async Task<IActionResult> DeactivateRole(int id)
        {
            var (ok, message) = await _roles.DeactivateRoleAsync(_tenantContext.CurrentTenantId, id, CurrentUserId);
            if (!ok) return BadRequest(new { success = false, message });
            return Ok(new { success = true, message });
        }
    }
}
