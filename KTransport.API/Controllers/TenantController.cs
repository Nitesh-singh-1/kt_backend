using System;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.DTOs;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TenantController : ControllerBase
    {
        private readonly ITenantService _tenantService;
        private readonly INavigationService _navService;
        private readonly ITenantContext _tenantContext;

        public TenantController(ITenantService tenantService, INavigationService navService, ITenantContext tenantContext)
        {
            _tenantService = tenantService;
            _navService = navService;
            _tenantContext = tenantContext;
        }

        /// <summary>
        /// Current tenant's usage vs. plan limits. Any authenticated user of the tenant can
        /// call this so the frontend can show plan-warning banners without needing
        /// super-user access. Tenant scope comes from the JWT — never a query parameter.
        /// </summary>
        [HttpGet("usage")]
        [Authorize]
        public async Task<ActionResult<TenantUsageDto>> GetMyUsage()
        {
            var snapshot = await _tenantService.GetUsageSnapshotAsync(_tenantContext.CurrentTenantId);
            return Ok(snapshot);
        }

        /// <summary>
        /// Onboard a new tenant organization, its administrator, and initial module pack.
        /// </summary>
        [HttpPost("onboard")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<ActionResult<TenantOnboardingResponse>> OnboardTenant([FromBody] TenantOnboardingRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _tenantService.OnboardTenantAsync(request);
            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Get all registered tenants with full SaaS details (Super User / System Admin only).
        /// </summary>
        [HttpGet]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<IActionResult> GetAllTenants()
        {
            var tenants = await _tenantService.GetAllTenantsWithDetailsAsync();
            return Ok(tenants);
        }

        /// <summary>
        /// Get tenant by ID (Super User only).
        /// </summary>
        [HttpGet("{id:guid}")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<IActionResult> GetTenantById(Guid id)
        {
            var tenant = await _tenantService.GetTenantByIdAsync(id);
            if (tenant == null)
            {
                return NotFound(new { message = "Tenant not found." });
            }

            return Ok(tenant);
        }

        /// <summary>
        /// Toggle tenant active/inactive status (Super User only).
        /// </summary>
        [HttpPut("{id:guid}/status")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<IActionResult> UpdateTenantStatus(Guid id, [FromBody] TenantStatusUpdateDto dto)
        {
            var success = await _tenantService.UpdateTenantStatusAsync(id, dto.IsActive);
            if (!success)
            {
                return NotFound(new { message = "Tenant not found." });
            }

            return Ok(new { success = true, message = $"Tenant status updated to {(dto.IsActive ? "Active" : "Inactive")}." });
        }

        /// <summary>
        /// Update tenant subscription plan tier (Super User only).
        /// </summary>
        [HttpPut("{id:guid}/plan")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<IActionResult> UpdateTenantPlan(Guid id, [FromBody] TenantPlanUpdateDto dto)
        {
            var success = await _tenantService.UpdateTenantSubscriptionPlanAsync(id, dto.PlanTier);
            if (!success)
            {
                return NotFound(new { message = "Tenant or plan not found." });
            }

            return Ok(new { success = true, message = $"Tenant subscription plan updated to {dto.PlanTier}." });
        }

        /// <summary>
        /// Get menu and sub-report entitlements for a specific tenant ID (Super User only).
        /// </summary>
        [HttpGet("{id:guid}/entitlements")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<ActionResult<TenantMenuEntitlementsDto>> GetTenantEntitlements(Guid id)
        {
            var entitlements = await _navService.GetTenantMenuEntitlementsAsync(id);
            return Ok(entitlements);
        }

        /// <summary>
        /// Update menu and sub-report entitlements for a specific tenant ID (Super User only).
        /// </summary>
        [HttpPut("{id:guid}/entitlements")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<ActionResult<TenantMenuEntitlementsDto>> UpdateTenantEntitlements(Guid id, [FromBody] TenantMenuEntitlementsDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var updated = await _navService.UpdateTenantMenuEntitlementsAsync(id, dto);
            return Ok(updated);
        }

        /// <summary>
        /// List the users of a specific tenant (Platform operator only).
        /// </summary>
        [HttpGet("{id:guid}/users")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<IActionResult> GetTenantUsers(Guid id)
        {
            var users = await _tenantService.GetTenantUsersAsync(id);
            return Ok(users);
        }

        /// <summary>
        /// Set a tenant user's role — e.g. promote to admin or demote to standard user (Platform operator only).
        /// Used to assign/repair an organization's administrator.
        /// </summary>
        [HttpPut("{id:guid}/users/{userId:int}/role")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<IActionResult> SetTenantUserRole(Guid id, int userId, [FromBody] SetUserRoleRequest dto)
        {
            var (success, message) = await _tenantService.SetTenantUserRoleAsync(id, userId, dto.Role);
            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }
    }
}
