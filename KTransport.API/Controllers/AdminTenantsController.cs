using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers
{
    /// <summary>
    /// TASK-046 Phase 1 — the platform-admin-only onboarding endpoint plus the
    /// system role templates endpoint used by the admin UI wizard.
    ///
    /// This is deliberately NOT TenantController, because the public
    /// POST /api/tenant/onboard route must keep its self-signup semantics
    /// (forced role='admin'). Splitting the endpoints into separate controllers
    /// prevents accidental sharing of DTO fields that would reintroduce the
    /// escalation hole this task closes.
    /// </summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize]
    [RequirePlatformAdmin]
    public class AdminTenantsController : ControllerBase
    {
        private readonly ITenantService _tenantService;

        public AdminTenantsController(ITenantService tenantService)
        {
            _tenantService = tenantService;
        }

        /// <summary>Platform-admin-only onboarding. Respects AdminRoleCode + EnabledModuleCodes.</summary>
        [HttpPost("tenants")]
        public async Task<ActionResult<TenantOnboardingResponse>> OnboardByAdmin([FromBody] TenantAdminOnboardRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int? platformUserId = null;
            var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (int.TryParse(sub, out var pid)) platformUserId = pid;

            var response = await _tenantService.OnboardTenantByAdminAsync(request, platformUserId);
            if (!response.Success) return BadRequest(response);
            return Ok(response);
        }

        /// <summary>Returns the 7 seeded system role templates (code + display name).</summary>
        [HttpGet("roles/system-templates")]
        public ActionResult<System.Collections.Generic.IReadOnlyList<SystemRoleTemplateDto>> GetSystemRoleTemplates()
        {
            return Ok(_tenantService.GetSystemRoleTemplates());
        }
    }
}
