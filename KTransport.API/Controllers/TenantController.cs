using System;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TenantController : ControllerBase
    {
        private readonly ITenantService _tenantService;
        private readonly INavigationService _navService;

        public TenantController(ITenantService tenantService, INavigationService navService)
        {
            _tenantService = tenantService;
            _navService = navService;
        }

        /// <summary>
        /// Onboard a new tenant organization, its administrator, and initial module pack.
        /// </summary>
        [HttpPost("onboard")]
        [AllowAnonymous]
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
        /// Get all registered tenants with full SaaS details (Superadmin / System Admin only).
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAllTenants()
        {
            var tenants = await _tenantService.GetAllTenantsWithDetailsAsync();
            return Ok(tenants);
        }

        /// <summary>
        /// Get tenant by ID.
        /// </summary>
        [HttpGet("{id:guid}")]
        [Authorize]
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
        /// Toggle tenant active/inactive status (Superadmin only).
        /// </summary>
        [HttpPut("{id:guid}/status")]
        [Authorize(Roles = "admin")]
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
        /// Update tenant subscription plan tier (Superadmin only).
        /// </summary>
        [HttpPut("{id:guid}/plan")]
        [Authorize(Roles = "admin")]
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
        /// Get menu and sub-report entitlements for a specific tenant ID.
        /// </summary>
        [HttpGet("{id:guid}/entitlements")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<TenantMenuEntitlementsDto>> GetTenantEntitlements(Guid id)
        {
            var entitlements = await _navService.GetTenantMenuEntitlementsAsync(id);
            return Ok(entitlements);
        }

        /// <summary>
        /// Update menu and sub-report entitlements for a specific tenant ID (Superadmin only).
        /// </summary>
        [HttpPut("{id:guid}/entitlements")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<TenantMenuEntitlementsDto>> UpdateTenantEntitlements(Guid id, [FromBody] TenantMenuEntitlementsDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var updated = await _navService.UpdateTenantMenuEntitlementsAsync(id, dto);
            return Ok(updated);
        }
    }
}
