using System;
using System.Threading.Tasks;
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

        public TenantController(ITenantService tenantService)
        {
            _tenantService = tenantService;
        }

        /// <summary>
        /// Onboard a new tenant organization and its initial administrator.
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
        /// Get all registered tenants (Superadmin / System Admin only).
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAllTenants()
        {
            var tenants = await _tenantService.GetAllTenantsAsync();
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
    }
}
