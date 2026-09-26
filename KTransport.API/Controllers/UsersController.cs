using System;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.DTOs;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequireSuperUser]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ITenantContext _tenantContext;
        private readonly IInvitationService _invitationService;

        public UsersController(IUserService userService, ITenantContext tenantContext, IInvitationService invitationService)
        {
            _userService = userService;
            _tenantContext = tenantContext;
            _invitationService = invitationService;
        }

        private int? CurrentUserId =>
            int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        /// <summary>Invite a teammate by email. They set their own username/password via the emailed link.</summary>
        [HttpPost("invite")]
        public async Task<IActionResult> InviteUser([FromBody] CreateInviteRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _invitationService.CreateInviteAsync(_tenantContext.CurrentTenantId, CurrentUserId, request);
            if (!result.Success) return BadRequest(new { success = false, message = result.Message });
            return Ok(new { success = true, message = result.Message, invite = result.Data });
        }

        /// <summary>List this organization's invitations (pending/accepted/revoked/expired).</summary>
        [HttpGet("invites")]
        public async Task<IActionResult> GetInvites()
        {
            var invites = await _invitationService.GetInvitesAsync(_tenantContext.CurrentTenantId);
            return Ok(invites);
        }

        /// <summary>Revoke a pending invitation.</summary>
        [HttpPost("invites/{id:long}/revoke")]
        public async Task<IActionResult> RevokeInvite(long id)
        {
            var result = await _invitationService.RevokeInviteAsync(_tenantContext.CurrentTenantId, id);
            if (!result.Success) return BadRequest(new { success = false, message = result.Message });
            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Get all sub-users belonging to the caller's organization.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var users = await _userService.GetTenantUsersAsync(tenantId);
            return Ok(users);
        }

        /// <summary>
        /// Get details and effective permissions for a specific sub-user.
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var user = await _userService.GetUserByIdAsync(tenantId, id);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }
            return Ok(user);
        }

        /// <summary>
        /// Create a new Sub User for the organization and assign allowed features.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateSubUser([FromBody] CreateSubUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = _tenantContext.CurrentTenantId;
            var result = await _userService.CreateSubUserAsync(tenantId, request);

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return CreatedAtAction(nameof(GetUserById), new { id = result.Data?.Id }, result.Data);
        }

        /// <summary>
        /// Update sub-user details (name, phone, role, active status).
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateSubUser(int id, [FromBody] UpdateSubUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = _tenantContext.CurrentTenantId;
            var result = await _userService.UpdateSubUserAsync(tenantId, id, request);

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(result.Data);
        }

        /// <summary>
        /// Assign or modify dedicated page / module permissions for a sub-user.
        /// </summary>
        [HttpPut("{id:int}/permissions")]
        public async Task<IActionResult> UpdateUserPermissions(int id, [FromBody] UpdateUserPermissionsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = _tenantContext.CurrentTenantId;
            var result = await _userService.UpdateUserPermissionsAsync(tenantId, id, request);

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Activate or deactivate a sub-user.
        /// </summary>
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateUserStatusRequest request)
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var result = await _userService.ToggleUserStatusAsync(tenantId, id, request.IsActive);

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Superadmin direct password reset for a sub-user.
        /// </summary>
        [HttpPost("{id:int}/reset-password")]
        public async Task<IActionResult> AdminResetPassword(int id, [FromBody] AdminResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.NewPassword))
            {
                return BadRequest(new { success = false, message = "New password is required." });
            }

            var tenantId = _tenantContext.CurrentTenantId;
            var result = await _userService.AdminResetUserPasswordAsync(tenantId, id, request.NewPassword);

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Delete / Deactivate a sub-user.
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var result = await _userService.DeleteUserAsync(tenantId, id);

            if (!result.Success)
            {
                return NotFound(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, message = result.Message });
        }
    }
}
