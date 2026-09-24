using KTransport.API.Authorization;
using KTransport.API.DTOs;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequireSuperUser]
    public class ConfigurationController : ControllerBase
    {
        private readonly ITenantConfigurationService _configService;
        private readonly ITenantContext _tenantContext;

        public ConfigurationController(ITenantConfigurationService configService, ITenantContext tenantContext)
        {
            _configService = configService;
            _tenantContext = tenantContext;
        }

        /// <summary>
        /// Public endpoint to get tenant branding and theme by tenant code/domain (for login or bootstrap).
        /// </summary>
        [HttpGet("public/{code}")]
        [AllowAnonymous]
        public async Task<ActionResult<PublicTenantConfigDto>> GetPublicConfig(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest(new { message = "Tenant code or domain is required." });
            }

            var config = await _configService.GetPublicTenantConfigAsync(code);
            if (config == null)
            {
                return NotFound(new { message = "Tenant configuration not found." });
            }

            return Ok(config);
        }

        /// <summary>
        /// Get full configuration for the currently authenticated tenant.
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<TenantConfigurationDto>> GetCurrentTenantConfiguration()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var config = await _configService.GetTenantConfigurationAsync(tenantId);
            return Ok(config);
        }

        /// <summary>
        /// Update configuration for the currently authenticated tenant.
        /// </summary>
        [HttpPut]
        [Authorize]
        public async Task<ActionResult<TenantConfigurationDto>> UpdateTenantConfiguration([FromBody] UpdateTenantConfigurationDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = _tenantContext.CurrentTenantId;
            var updatedConfig = await _configService.UpdateTenantConfigurationAsync(tenantId, dto);
            return Ok(updatedConfig);
        }

        /// <summary>
        /// Get active feature flags and module access matrix for the currently authenticated tenant.
        /// </summary>
        [HttpGet("features")]
        [Authorize]
        public async Task<ActionResult<TenantFeatureFlagsDto>> GetFeatureFlags()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var flags = await _configService.GetFeatureFlagsAsync(tenantId);
            return Ok(flags);
        }

        /// <summary>
        /// Get subscription plan, resource limits, and current usage for the tenant.
        /// </summary>
        [HttpGet("subscription")]
        [Authorize]
        public async Task<ActionResult<TenantSubscriptionDto>> GetSubscription()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var subscription = await _configService.GetSubscriptionDetailsAsync(tenantId);
            return Ok(subscription);
        }

        /// <summary>
        /// Reset tenant configuration to system default settings.
        /// </summary>
        [HttpPost("reset")]
        [Authorize]
        public async Task<ActionResult<TenantConfigurationDto>> ResetToDefaults()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var config = await _configService.ResetToDefaultsAsync(tenantId);
            return Ok(config);
        }
    }
}
