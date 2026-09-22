using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NavigationController : ControllerBase
    {
        private readonly INavigationService _navService;
        private readonly ITenantContext _tenantContext;
        private readonly KTransportDbContext _context;

        public NavigationController(
            INavigationService navService, 
            ITenantContext tenantContext,
            KTransportDbContext context)
        {
            _navService = navService;
            _tenantContext = tenantContext;
            _context = context;
        }

        /// <summary>
        /// Get the dynamic hierarchical menu configured for the active tenant and caller's role.
        /// </summary>
        [HttpGet("menu")]
        [AllowAnonymous]
        public async Task<ActionResult<List<DynamicMenuItemDto>>> GetMenu()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var role = User.FindFirst(ClaimTypes.Role)?.Value 
                       ?? User.FindFirst("role")?.Value 
                       ?? "admin";

            var menu = await _navService.GetDynamicMenuAsync(tenantId, role);
            return Ok(menu);
        }

        /// <summary>
        /// Get active permission keys for the caller (for UI element gating and route protection).
        /// </summary>
        [HttpGet("permissions")]
        [AllowAnonymous]
        public async Task<ActionResult<List<string>>> GetPermissions()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var role = User.FindFirst(ClaimTypes.Role)?.Value 
                       ?? User.FindFirst("role")?.Value 
                       ?? "admin";

            var permissions = await _navService.GetUserPermissionsAsync(tenantId, role);
            return Ok(permissions);
        }

        /// <summary>
        /// Get all onboarded clients/tenants (for multi-tenant administration).
        /// </summary>
        [HttpGet("/api/configuration/tenants")]
        [Authorize]
        public async Task<IActionResult> GetAllTenants()
        {
            var tenants = await _context.Tenants
                .IgnoreQueryFilters()
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Code,
                    t.IsActive,
                    t.CreatedAt
                })
                .OrderBy(t => t.Name)
                .ToListAsync();

            return Ok(tenants);
        }

        /// <summary>
        /// Get the full menu, pages, and sub-reports entitlement catalog for current authenticated tenant.
        /// </summary>
        [HttpGet("/api/configuration/menu-entitlements")]
        [Authorize]
        public async Task<ActionResult<TenantMenuEntitlementsDto>> GetEntitlements()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var entitlements = await _navService.GetTenantMenuEntitlementsAsync(tenantId);
            return Ok(entitlements);
        }

        /// <summary>
        /// Get menu, pages, and sub-reports entitlement catalog for a specific tenant ID (Client A, Client B, etc.).
        /// </summary>
        [HttpGet("/api/configuration/tenants/{tenantId:guid}/menu-entitlements")]
        [Authorize]
        public async Task<ActionResult<TenantMenuEntitlementsDto>> GetEntitlementsForTenant(Guid tenantId)
        {
            var entitlements = await _navService.GetTenantMenuEntitlementsAsync(tenantId);
            return Ok(entitlements);
        }

        /// <summary>
        /// Update menu, page, and report entitlements for the current tenant.
        /// </summary>
        [HttpPut("/api/configuration/menu-entitlements")]
        [Authorize]
        public async Task<ActionResult<TenantMenuEntitlementsDto>> UpdateEntitlements([FromBody] TenantMenuEntitlementsDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = _tenantContext.CurrentTenantId;
            var updated = await _navService.UpdateTenantMenuEntitlementsAsync(tenantId, dto);
            return Ok(updated);
        }

        /// <summary>
        /// Update menu, page, and report entitlements for a specific tenant ID (Client A, Client B, etc.).
        /// </summary>
        [HttpPut("/api/configuration/tenants/{tenantId:guid}/menu-entitlements")]
        [Authorize]
        public async Task<ActionResult<TenantMenuEntitlementsDto>> UpdateEntitlementsForTenant(Guid tenantId, [FromBody] TenantMenuEntitlementsDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var updated = await _navService.UpdateTenantMenuEntitlementsAsync(tenantId, dto);
            return Ok(updated);
        }
    }
}
